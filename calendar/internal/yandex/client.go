package yandex

import (
	"bytes"
	"context"
	"encoding/json"
	"errors"
	"fmt"
	"io"
	"net/http"
	"regexp"
	"strings"
	"time"
)

type Client struct {
	cookieHeader string
	uid          string
	mailCKey     string
	calendarCKey string
	timezone     string
	httpClient   *http.Client
}

type BusyInterval struct {
	Start time.Time
	End   time.Time
}

var (
	mailCKeyRegex     = regexp.MustCompile(`"ckey"\s*:\s*"([^"]+)"`)
	calendarCKeyRegex = regexp.MustCompile(`"ckey"\s*:\s*"([^"]+)"`)
)

// NewClient creates a Yandex API client using the raw cookie header string.
func NewClient(cookieHeader, timezone string) (*Client, error) {
	cookieHeader = strings.TrimSpace(cookieHeader)
	if cookieHeader == "" {
		return nil, errors.New("yandex cookie is empty")
	}

	uid := extractCookieValue(cookieHeader, "yandexuid")
	if uid == "" {
		return nil, errors.New("yandexuid cookie is missing")
	}

	if timezone == "" {
		timezone = "Europe/Moscow"
	}

	return &Client{
		cookieHeader: cookieHeader,
		uid:          uid,
		timezone:     timezone,
		httpClient:   &http.Client{Timeout: 10 * time.Second},
	}, nil
}

func extractCookieValue(cookieHeader, key string) string {
	parts := strings.Split(cookieHeader, ";")
	for _, part := range parts {
		item := strings.SplitN(strings.TrimSpace(part), "=", 2)
		if len(item) != 2 {
			continue
		}
		if item[0] == key {
			return item[1]
		}
	}
	return ""
}

func (c *Client) SearchEmailByName(ctx context.Context, query string) (string, error) {
	if strings.TrimSpace(query) == "" {
		return "", errors.New("query is empty")
	}

	for attempt := 0; attempt < 2; attempt++ {
		if err := c.ensureMailCKey(ctx); err != nil {
			return "", err
		}

		payload := map[string]any{
			"models": []any{
				map[string]any{
					"name":   "abook-contacts",
					"params": map[string]string{"pagesize": "5", "q": query, "type": "normal"},
					"meta":   map[string]int{"requestAttempt": 1},
				},
			},
			"_ckey": c.mailCKey,
			"_uid":  c.uid,
		}

		respBody, status, err := c.doJSON(ctx, http.MethodPost, "https://mail.yandex.ru/web-api/models/liza1?_m=abook-contacts", payload, defaultHeaders())
		if err != nil {
			return "", err
		}
		if status != http.StatusOK {
			return "", fmt.Errorf("mail api status %d", status)
		}

		var response mailResponse
		if err := json.Unmarshal(respBody, &response); err != nil {
			return "", err
		}

		model := response.FirstModel()
		if model == nil {
			return "", nil
		}

		if model.Status == "error" {
			c.mailCKey = ""
			continue
		}

		for _, contact := range model.Data.Contact {
			if len(contact.Email) == 0 {
				continue
			}
			value := strings.TrimSpace(contact.Email[0].Value)
			if value != "" {
				return value, nil
			}
		}

		return "", nil
	}

	return "", nil
}

func (c *Client) GetBusyIntervals(ctx context.Context, email string, date time.Time) ([]BusyInterval, error) {
	email = strings.TrimSpace(email)
	if email == "" {
		return nil, errors.New("email is empty")
	}

	for attempt := 0; attempt < 2; attempt++ {
		if err := c.ensureCalendarCKey(ctx); err != nil {
			return nil, err
		}

		start := date.Format("2006-01-02")
		end := date.Add(24 * time.Hour).Format("2006-01-02")

		payload := map[string]any{
			"models": []any{
				map[string]any{
					"name": "get-events-by-login",
					"params": map[string]any{
						"limitAttendees": true,
						"login":          email,
						"opaqueOnly":     true,
						"email":          email,
						"from":           start,
						"to":             end,
					},
				},
			},
		}

		headers := defaultHeaders()
		headers["x-yandex-maya-ckey"] = c.calendarCKey
		headers["x-yandex-maya-uid"] = c.uid
		headers["x-yandex-maya-cid"] = fmt.Sprintf("MAYA-%d", time.Now().UnixMilli())
		headers["x-yandex-maya-timezone"] = c.timezone

		respBody, status, err := c.doJSON(ctx, http.MethodPost, "https://calendar.yandex.ru/api/models?_models=get-events-by-login", payload, headers)
		if err != nil {
			return nil, err
		}
		if status != http.StatusOK {
			return nil, fmt.Errorf("calendar api status %d", status)
		}

		var response calendarResponse
		if err := json.Unmarshal(respBody, &response); err != nil {
			return nil, err
		}

		model := response.FirstModel()
		if model == nil {
			return []BusyInterval{}, nil
		}

		if model.Status == "error" {
			c.calendarCKey = ""
			continue
		}

		loc, err := time.LoadLocation(c.timezone)
		if err != nil {
			loc = time.UTC
		}

		intervals := make([]BusyInterval, 0, len(model.Data.Events))
		for _, event := range model.Data.Events {
			if event.Decision == "no" || event.Availability == "free" {
				continue
			}
			startTime, err := time.Parse(time.RFC3339, event.Start)
			if err != nil {
				continue
			}
			endTime, err := time.Parse(time.RFC3339, event.End)
			if err != nil {
				continue
			}

			intervals = append(intervals, BusyInterval{
				Start: startTime.In(loc),
				End:   endTime.In(loc),
			})
		}

		return intervals, nil
	}

	return []BusyInterval{}, nil
}

func (c *Client) ensureMailCKey(ctx context.Context) error {
	if c.mailCKey != "" {
		return nil
	}

	body, status, err := c.doRequest(ctx, http.MethodGet, fmt.Sprintf("https://mail.yandex.ru/?uid=%s", c.uid), nil, defaultHeaders())
	if err != nil {
		return err
	}
	if status != http.StatusOK {
		return fmt.Errorf("mail ckey status %d", status)
	}

	match := mailCKeyRegex.FindStringSubmatch(body)
	if len(match) < 2 {
		return errors.New("mail ckey not found")
	}
	c.mailCKey = match[1]
	return nil
}

func (c *Client) ensureCalendarCKey(ctx context.Context) error {
	if c.calendarCKey != "" {
		return nil
	}

	body, status, err := c.doRequest(ctx, http.MethodGet, fmt.Sprintf("https://calendar.yandex.ru/?uid=%s", c.uid), nil, defaultHeaders())
	if err != nil {
		return err
	}
	if status != http.StatusOK {
		return fmt.Errorf("calendar ckey status %d", status)
	}

	match := calendarCKeyRegex.FindStringSubmatch(body)
	if len(match) < 2 {
		return errors.New("calendar ckey not found")
	}
	c.calendarCKey = match[1]
	return nil
}

func defaultHeaders() map[string]string {
	return map[string]string{
		"User-Agent":       "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
		"Accept":           "application/json, text/javascript, */*; q=0.01",
		"Accept-Language":  "ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7",
		"Content-Type":     "application/json",
		"X-Requested-With": "XMLHttpRequest",
		"Origin":           "https://calendar.yandex.ru",
		"Referer":          "https://calendar.yandex.ru/",
	}
}

func (c *Client) doJSON(ctx context.Context, method, url string, payload any, headers map[string]string) ([]byte, int, error) {
	body, err := json.Marshal(payload)
	if err != nil {
		return nil, 0, err
	}

	respBody, status, err := c.doRequest(ctx, method, url, bytes.NewReader(body), headers)
	if err != nil {
		return nil, 0, err
	}
	return []byte(respBody), status, nil
}

func (c *Client) doRequest(ctx context.Context, method, url string, body io.Reader, headers map[string]string) (string, int, error) {
	req, err := http.NewRequestWithContext(ctx, method, url, body)
	if err != nil {
		return "", 0, err
	}
	for key, value := range headers {
		req.Header.Set(key, value)
	}
	req.Header.Set("Cookie", c.cookieHeader)

	resp, err := c.httpClient.Do(req)
	if err != nil {
		return "", 0, err
	}
	defer resp.Body.Close()

	payload, err := io.ReadAll(resp.Body)
	if err != nil {
		return "", resp.StatusCode, err
	}

	return string(payload), resp.StatusCode, nil
}

type mailResponse struct {
	Models []mailModel `json:"models"`
}

type mailModel struct {
	Status string   `json:"status"`
	Error  string   `json:"error"`
	Data   mailData `json:"data"`
}

type mailData struct {
	Contact []mailContact `json:"contact"`
}

type mailContact struct {
	Email []mailEmail `json:"email"`
}

type mailEmail struct {
	Value string `json:"value"`
}

func (r *mailResponse) FirstModel() *mailModel {
	if len(r.Models) == 0 {
		return nil
	}
	return &r.Models[0]
}

type calendarResponse struct {
	Models []calendarModel `json:"models"`
}

type calendarModel struct {
	Status string       `json:"status"`
	Error  string       `json:"error"`
	Data   calendarData `json:"data"`
}

type calendarData struct {
	Events []calendarEvent `json:"events"`
}

type calendarEvent struct {
	Start        string `json:"start"`
	End          string `json:"end"`
	Decision     string `json:"decision"`
	Availability string `json:"availability"`
}

func (r *calendarResponse) FirstModel() *calendarModel {
	if len(r.Models) == 0 {
		return nil
	}
	return &r.Models[0]
}
