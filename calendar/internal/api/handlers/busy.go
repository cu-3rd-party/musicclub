package handlers

import (
	"database/sql"
	"net/http"
	"strconv"
	"strings"
	"time"

	"github.com/gin-gonic/gin"

	"musicclubbot/calendar/internal/yandex"
	"musicclubbot/calendar/pkg/db"
)

type BusyInterval struct {
	Start time.Time `json:"start"`
	End   time.Time `json:"end"`
}

type BusyResponse struct {
	Email     string         `json:"email"`
	Intervals []BusyInterval `json:"intervals"`
}

type CalendarHandler struct {
	store  *db.CalendarStore
	yandex *yandex.Client
}

// NewCalendarHandler builds a handler wrapper with required dependencies.
func NewCalendarHandler(store *db.CalendarStore, yandexClient *yandex.Client) *CalendarHandler {
	return &CalendarHandler{store: store, yandex: yandexClient}
}

// BusyIntervals returns busy intervals for a user identified by query parameters.
func (h *CalendarHandler) BusyIntervals(c *gin.Context) {
	if h.store == nil {
		c.JSON(http.StatusInternalServerError, gin.H{"error": "calendar store is not configured"})
		return
	}
	if h.yandex == nil {
		c.JSON(http.StatusInternalServerError, gin.H{"error": "yandex calendar is not configured"})
		return
	}

	dateStr := strings.TrimSpace(c.Query("date"))
	if dateStr == "" {
		c.JSON(http.StatusBadRequest, gin.H{"error": "date query parameter is required (YYYY-MM-DD)"})
		return
	}
	date, err := time.Parse("2006-01-02", dateStr)
	if err != nil {
		c.JSON(http.StatusBadRequest, gin.H{"error": "invalid date format (YYYY-MM-DD)"})
		return
	}

	email := strings.TrimSpace(c.Query("email"))
	tgID := strings.TrimSpace(c.Query("tg_id"))
	name := strings.TrimSpace(c.Query("name"))

	if email == "" && tgID == "" && name == "" {
		c.JSON(http.StatusBadRequest, gin.H{"error": "email, tg_id, or name query parameter is required"})
		return
	}

	if email == "" && tgID != "" {
		id, err := strconv.ParseInt(tgID, 10, 64)
		if err != nil {
			c.JSON(http.StatusBadRequest, gin.H{"error": "invalid tg_id"})
			return
		}
		email, err = h.store.GetEmailByTelegramID(c.Request.Context(), id)
		if err == sql.ErrNoRows {
			c.JSON(http.StatusNotFound, gin.H{"error": "email not found for tg_id"})
			return
		}
		if err != nil {
			c.JSON(http.StatusInternalServerError, gin.H{"error": "failed to lookup email by tg_id"})
			return
		}
	}

	if email == "" && name != "" {
		storedEmail, err := h.store.GetEmailByName(c.Request.Context(), name)
		if err == nil {
			email = storedEmail
		} else if err != sql.ErrNoRows {
			c.JSON(http.StatusInternalServerError, gin.H{"error": "failed to lookup email by name"})
			return
		}

		if email == "" {
			email, err = h.yandex.SearchEmailByName(c.Request.Context(), name)
			if err != nil {
				c.JSON(http.StatusBadGateway, gin.H{"error": "failed to resolve email by name"})
				return
			}
			if email == "" {
				c.JSON(http.StatusNotFound, gin.H{"error": "email not found for name"})
				return
			}
		}
	}

	intervals, err := h.yandex.GetBusyIntervals(c.Request.Context(), email, date)
	if err != nil {
		c.JSON(http.StatusBadGateway, gin.H{"error": "failed to fetch busy intervals"})
		return
	}

	respIntervals := make([]BusyInterval, 0, len(intervals))
	for _, interval := range intervals {
		respIntervals = append(respIntervals, BusyInterval{Start: interval.Start, End: interval.End})
	}

	c.JSON(http.StatusOK, BusyResponse{
		Email:     email,
		Intervals: respIntervals,
	})
}
