package app

import (
	"context"
	"database/sql"
	"net/http"
	"time"

	"github.com/prometheus/client_golang/prometheus"
	"github.com/prometheus/client_golang/prometheus/promhttp"
	"google.golang.org/grpc"
	"google.golang.org/grpc/status"
)

var (
	grpcRequestsTotal = prometheus.NewCounterVec(
		prometheus.CounterOpts{
			Name: "grpc_requests_total",
			Help: "Total number of gRPC requests.",
		},
		[]string{"method", "code"},
	)
	grpcRequestDuration = prometheus.NewHistogramVec(
		prometheus.HistogramOpts{
			Name:    "grpc_request_duration_seconds",
			Help:    "Histogram of gRPC request durations in seconds.",
			Buckets: prometheus.ExponentialBuckets(0.005, 2, 12),
		},
		[]string{"method", "code"},
	)
	businessUsersTotal = prometheus.NewGauge(
		prometheus.GaugeOpts{
			Name: "musicclub_users_total",
			Help: "Total number of users.",
		},
	)
	businessSongsTotal = prometheus.NewGauge(
		prometheus.GaugeOpts{
			Name: "musicclub_songs_total",
			Help: "Total number of songs.",
		},
	)
	businessSongRolesTotal = prometheus.NewGauge(
		prometheus.GaugeOpts{
			Name: "musicclub_song_roles_total",
			Help: "Total number of song roles.",
		},
	)
	businessSongRoleAssignmentsTotal = prometheus.NewGauge(
		prometheus.GaugeOpts{
			Name: "musicclub_song_role_assignments_total",
			Help: "Total number of song role assignments.",
		},
	)
	businessSongRoleAssignedUsersTotal = prometheus.NewGauge(
		prometheus.GaugeOpts{
			Name: "musicclub_song_role_assigned_users_total",
			Help: "Total number of users assigned to at least one song role.",
		},
	)
	businessFilledSongsTotal = prometheus.NewGauge(
		prometheus.GaugeOpts{
			Name: "musicclub_filled_songs_total",
			Help: "Total number of songs with all roles filled.",
		},
	)
)

func init() {
	prometheus.MustRegister(
		grpcRequestsTotal,
		grpcRequestDuration,
		businessUsersTotal,
		businessSongsTotal,
		businessSongRolesTotal,
		businessSongRoleAssignmentsTotal,
		businessSongRoleAssignedUsersTotal,
		businessFilledSongsTotal,
	)
}

func metricsInterceptor(
	ctx context.Context,
	req any,
	info *grpc.UnaryServerInfo,
	handler grpc.UnaryHandler,
) (any, error) {
	start := time.Now()
	resp, err := handler(ctx, req)
	duration := time.Since(start).Seconds()
	code := status.Code(err).String()

	grpcRequestsTotal.WithLabelValues(info.FullMethod, code).Inc()
	grpcRequestDuration.WithLabelValues(info.FullMethod, code).Observe(duration)

	return resp, err
}

func newMetricsHandler() http.Handler {
	mux := http.NewServeMux()
	mux.Handle("/metrics", promhttp.Handler())
	return mux
}

func startBusinessMetricsCollector(ctx context.Context) {
	log := mustLog(ctx)
	dbValue := ctx.Value("db")
	db, ok := dbValue.(*sql.DB)
	if !ok || db == nil {
		log.Warning("Business metrics collector disabled: database connection not available")
		return
	}

	ticker := time.NewTicker(30 * time.Second)
	defer ticker.Stop()

	for {
		if err := collectBusinessMetrics(ctx, db); err != nil {
			log.Warningf("Business metrics collection failed: %v", err)
		}

		select {
		case <-ctx.Done():
			return
		case <-ticker.C:
		}
	}
}

func collectBusinessMetrics(ctx context.Context, db *sql.DB) error {
	metricsCtx, cancel := context.WithTimeout(ctx, 5*time.Second)
	defer cancel()

	var count int
	if err := db.QueryRowContext(metricsCtx, `SELECT COUNT(*) FROM app_user`).Scan(&count); err != nil {
		return err
	}
	businessUsersTotal.Set(float64(count))

	if err := db.QueryRowContext(metricsCtx, `SELECT COUNT(*) FROM song`).Scan(&count); err != nil {
		return err
	}
	businessSongsTotal.Set(float64(count))

	if err := db.QueryRowContext(metricsCtx, `SELECT COUNT(*) FROM song_role`).Scan(&count); err != nil {
		return err
	}
	businessSongRolesTotal.Set(float64(count))

	if err := db.QueryRowContext(metricsCtx, `SELECT COUNT(*) FROM song_role_assignment`).Scan(&count); err != nil {
		return err
	}
	businessSongRoleAssignmentsTotal.Set(float64(count))

	if err := db.QueryRowContext(metricsCtx, `SELECT COUNT(DISTINCT user_id) FROM song_role_assignment`).Scan(&count); err != nil {
		return err
	}
	businessSongRoleAssignedUsersTotal.Set(float64(count))

	if err := db.QueryRowContext(metricsCtx, `
		SELECT COUNT(*) FROM (
			SELECT s.id,
			       COUNT(sr.role) AS total_roles,
			       COUNT(DISTINCT sra.role) AS filled_roles
			FROM song s
			LEFT JOIN song_role sr ON sr.song_id = s.id
			LEFT JOIN song_role_assignment sra
				ON sra.song_id = s.id AND sra.role = sr.role
			GROUP BY s.id
		) AS song_counts
		WHERE total_roles > 0 AND filled_roles >= total_roles
	`).Scan(&count); err != nil {
		return err
	}
	businessFilledSongsTotal.Set(float64(count))

	return nil
}
