package helpers

import (
	"regexp"
	"strings"
)

// ExtractThumbnailURL extracts a thumbnail URL from a song link based on the link type.
// Returns empty string if thumbnail cannot be extracted.
func ExtractThumbnailURL(linkKind, linkURL string) string {
	switch linkKind {
	case "youtube":
		return extractYouTubeThumbnail(linkURL)
	case "yandex_music":
		// Yandex Music doesn't have a simple thumbnail URL pattern
		return ""
	case "soundcloud":
		// SoundCloud requires API calls to get thumbnails
		return ""
	default:
		return ""
	}
}

// extractYouTubeThumbnail extracts thumbnail URL from YouTube link.
// Supports youtube.com/watch?v=ID and youtu.be/ID formats.
func extractYouTubeThumbnail(url string) string {
	videoID := extractYouTubeVideoID(url)
	if videoID == "" {
		return ""
	}
	// Use maxresdefault for highest quality, fallback to hqdefault in frontend if needed
	return "https://img.youtube.com/vi/" + videoID + "/maxresdefault.jpg"
}

// extractYouTubeVideoID extracts video ID from various YouTube URL formats.
func extractYouTubeVideoID(url string) string {
	// Pattern 1: youtube.com/watch?v=VIDEO_ID
	re1 := regexp.MustCompile(`(?:youtube\.com/watch\?v=|youtu\.be/)([a-zA-Z0-9_-]{11})`)
	matches := re1.FindStringSubmatch(url)
	if len(matches) >= 2 {
		return matches[1]
	}

	// Pattern 2: youtube.com/embed/VIDEO_ID
	re2 := regexp.MustCompile(`youtube\.com/embed/([a-zA-Z0-9_-]{11})`)
	matches = re2.FindStringSubmatch(url)
	if len(matches) >= 2 {
		return matches[1]
	}

	// Pattern 3: youtube.com/v/VIDEO_ID
	re3 := regexp.MustCompile(`youtube\.com/v/([a-zA-Z0-9_-]{11})`)
	matches = re3.FindStringSubmatch(url)
	if len(matches) >= 2 {
		return matches[1]
	}

	return ""
}

// NormalizeThumbnailURL returns the provided custom URL if not empty,
// otherwise attempts to extract from the link.
func NormalizeThumbnailURL(customURL, linkKind, linkURL string) string {
	customURL = strings.TrimSpace(customURL)
	if customURL != "" {
		return customURL
	}
	return ExtractThumbnailURL(linkKind, linkURL)
}
