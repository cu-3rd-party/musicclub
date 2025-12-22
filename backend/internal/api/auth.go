package api

import (
	"context"
	"crypto/rand"
	"database/sql"
	"encoding/base64"
	"fmt"
	"strings"
	"time"

	"github.com/golang-jwt/jwt/v5"
	"github.com/google/uuid"
	"google.golang.org/grpc"
	"google.golang.org/grpc/codes"
	"google.golang.org/grpc/metadata"
	"google.golang.org/grpc/status"

	"golang.org/x/crypto/bcrypt"

	"musicclubbot/backend/internal/config"
	authpb "musicclubbot/backend/proto"
	permissionspb "musicclubbot/backend/proto"
	userpb "musicclubbot/backend/proto"

	emptypb "google.golang.org/protobuf/types/known/emptypb"
)

// JWT configuration
const (
	accessTokenExp   = 15 * time.Minute   // 15 minutes
	refreshTokenExp  = 7 * 24 * time.Hour // 7 days
	refreshTokenSize = 32                 // bytes for refresh token
)

type JWTClaims struct {
	UserID   string `json:"user_id"`
	Username string `json:"username"`
	jwt.RegisteredClaims
}

// Refresh tokens table structure
type RefreshToken struct {
	ID        string    `db:"id"`
	UserID    string    `db:"user_id"`
	Token     string    `db:"token"`
	ExpiresAt time.Time `db:"expires_at"`
	CreatedAt time.Time `db:"created_at"`
}

func hashPassword(password string) (string, error) {
	hashedBytes, err := bcrypt.GenerateFromPassword([]byte(password), bcrypt.DefaultCost)
	if err != nil {
		return "", err
	}
	return string(hashedBytes), nil
}

func checkPasswordHash(password, hash string) bool {
	err := bcrypt.CompareHashAndPassword([]byte(hash), []byte(password))
	return err == nil
}

func generateAccessToken(ctx context.Context, userID uuid.UUID, username string) (string, error) {
	cfg := ctx.Value("cfg").(config.Config)
	expirationTime := time.Now().Add(accessTokenExp)

	claims := &JWTClaims{
		UserID:   userID.String(),
		Username: username,
		RegisteredClaims: jwt.RegisteredClaims{
			ExpiresAt: jwt.NewNumericDate(expirationTime),
			IssuedAt:  jwt.NewNumericDate(time.Now()),
			Issuer:    "musicclubbot",
			Subject:   userID.String(),
		},
	}

	token := jwt.NewWithClaims(jwt.SigningMethodHS256, claims)
	return token.SignedString(cfg.JwtSecretKey)
}

func generateRefreshToken() (string, error) {
	// Generate a secure random string for refresh token
	tokenBytes := make([]byte, refreshTokenSize)
	if _, err := rand.Read(tokenBytes); err != nil {
		return "", err
	}
	return base64.URLEncoding.EncodeToString(tokenBytes), nil
}

func verifyToken(ctx context.Context, tokenString string) (*JWTClaims, error) {
	cfg := ctx.Value("cfg").(config.Config)
	token, err := jwt.ParseWithClaims(tokenString, &JWTClaims{}, func(token *jwt.Token) (interface{}, error) {
		if _, ok := token.Method.(*jwt.SigningMethodHMAC); !ok {
			return nil, fmt.Errorf("unexpected signing method: %v", token.Header["alg"])
		}
		return cfg.JwtSecretKey, nil
	})

	if err != nil {
		return nil, err
	}

	if claims, ok := token.Claims.(*JWTClaims); ok && token.Valid {
		return claims, nil
	}

	return nil, fmt.Errorf("invalid token")
}

// AuthService implements auth-related gRPC endpoints.
type AuthService struct {
	authpb.UnimplementedAuthServiceServer
	// You might want to add dependencies like a Telegram bot client here
	// telegramBot *tgbotapi.BotAPI
}

func (s *AuthService) Register(ctx context.Context, req *authpb.RegisterUserRequest) (*authpb.AuthSession, error) {
	db, err := dbFromCtx(ctx)
	if err != nil {
		return nil, status.Error(codes.Internal, err.Error())
	}

	username := req.GetCredentials().GetUsername()
	if username == "" {
		return nil, status.Error(codes.InvalidArgument, "username is required")
	}

	var exists bool
	err = db.QueryRowContext(ctx,
		`SELECT EXISTS(SELECT 1 FROM app_user WHERE username = $1)`,
		username,
	).Scan(&exists)

	if err != nil {
		return nil, status.Errorf(codes.Internal, "check existing username: %v", err)
	}
	if exists {
		return nil, status.Error(codes.AlreadyExists, "username already taken")
	}

	password := req.GetCredentials().GetPassword()
	if !acceptablePassword(password) {
		return nil, status.Error(codes.InvalidArgument, "password does not meet complexity requirements")
	}

	hashedPassword, err := hashPassword(password)
	if err != nil {
		return nil, status.Errorf(codes.Internal, "hash password: %v", err)
	}

	tx, err := db.BeginTx(ctx, nil)
	if err != nil {
		return nil, status.Errorf(codes.Internal, "begin tx: %v", err)
	}
	defer tx.Rollback()

	var userID uuid.UUID
	var displayName string
	var avatarUrl *string

	profile := req.GetProfile()
	if profile != nil {
		displayName = profile.GetDisplayName()
		if profile.GetAvatarUrl() != "" {
			avatarUrl = &profile.AvatarUrl
		}
	}

	// Use default display name if not provided
	if displayName == "" {
		displayName = username
	}

	err = tx.QueryRowContext(ctx, `
		INSERT INTO app_user (username, password_hash, display_name, avatar_url, is_chat_member) 
		VALUES ($1, $2, $3, $4, FALSE)
		RETURNING id, display_name, avatar_url`,
		username,
		hashedPassword,
		displayName,
		avatarUrl,
	).Scan(&userID, &displayName, &avatarUrl)

	if err != nil {
		return nil, status.Errorf(codes.Internal, "insert user: %v", err)
	}

	// челику без тг запрещено все
	_, err = tx.ExecContext(ctx, `
		INSERT INTO user_permissions (user_id, edit_own_participation, edit_any_participation, 
		                              edit_own_songs, edit_any_songs, edit_events, edit_tracklists)
		VALUES ($1, FALSE, FALSE, FALSE, FALSE, FALSE, FALSE)`,
		userID,
	)

	if err != nil {
		return nil, status.Errorf(codes.Internal, "set default permissions: %v", err)
	}

	// Generate JWT tokens
	accessToken, err := generateAccessToken(ctx, userID, username)
	if err != nil {
		return nil, status.Errorf(codes.Internal, "generate access token: %v", err)
	}

	refreshToken, err := generateRefreshToken()
	if err != nil {
		return nil, status.Errorf(codes.Internal, "generate refresh token: %v", err)
	}

	// Store refresh token in database
	refreshExpiresAt := time.Now().Add(refreshTokenExp)
	_, err = tx.ExecContext(ctx, `
		INSERT INTO refresh_tokens (id, user_id, token, expires_at)
		VALUES (gen_random_uuid(), $1, $2, $3)`,
		userID, refreshToken, refreshExpiresAt)

	if err != nil {
		return nil, status.Errorf(codes.Internal, "store refresh token: %v", err)
	}

	// Get permissions for response
	permissions, err := getUserPermissions(ctx, tx, userID)
	if err != nil {
		return nil, status.Errorf(codes.Internal, "get user permissions: %v", err)
	}

	if err := tx.Commit(); err != nil {
		return nil, status.Errorf(codes.Internal, "commit: %v", err)
	}

	// Create user profile response
	profileResp := &userpb.User{
		Id:          userID.String(),
		Username:    username,
		DisplayName: displayName,
	}
	if avatarUrl != nil {
		profileResp.AvatarUrl = *avatarUrl
	}

	// Check if user is chat member
	var isChatMember bool
	err = db.QueryRowContext(ctx,
		`SELECT is_chat_member FROM app_user WHERE id = $1`,
		userID,
	).Scan(&isChatMember)

	if err != nil {
		isChatMember = false
	}

	return &authpb.AuthSession{
		Tokens: &authpb.TokenPair{
			AccessToken:  accessToken,
			RefreshToken: refreshToken,
		},
		Iat:            uint64(time.Now().Unix()),
		Exp:            uint64(time.Now().Add(accessTokenExp).Unix()),
		IsChatMember:   isChatMember,
		JoinRequestUrl: "https://t.me/your_musicclub_bot?start=join", // Replace with your bot
		Profile:        profileResp,
		Permissions:    permissions,
	}, nil
}

func (s *AuthService) Login(ctx context.Context, req *authpb.Credentials) (*authpb.AuthSession, error) {
	db, err := dbFromCtx(ctx)
	if err != nil {
		return nil, status.Error(codes.Internal, err.Error())
	}

	username := req.GetUsername()
	password := req.GetPassword()

	if username == "" || password == "" {
		return nil, status.Error(codes.InvalidArgument, "username and password are required")
	}

	// Get user from database
	var userID uuid.UUID
	var hashedPassword string
	var displayName string
	var avatarUrl sql.NullString
	var isChatMember bool
	var createdAt time.Time

	err = db.QueryRowContext(ctx, `
		SELECT id, password_hash, display_name, avatar_url, is_chat_member, created_at
		FROM app_user 
		WHERE username = $1`,
		username,
	).Scan(&userID, &hashedPassword, &displayName, &avatarUrl, &isChatMember, &createdAt)

	if err != nil {
		if err == sql.ErrNoRows {
			return nil, status.Error(codes.Unauthenticated, "invalid credentials")
		}
		return nil, status.Errorf(codes.Internal, "query user: %v", err)
	}

	// Verify password
	if !checkPasswordHash(password, hashedPassword) {
		return nil, status.Error(codes.Unauthenticated, "invalid credentials")
	}

	// Generate new tokens
	accessToken, err := generateAccessToken(ctx, userID, username)
	if err != nil {
		return nil, status.Errorf(codes.Internal, "generate access token: %v", err)
	}

	refreshToken, err := generateRefreshToken()
	if err != nil {
		return nil, status.Errorf(codes.Internal, "generate refresh token: %v", err)
	}

	// Store refresh token and invalidate old ones
	tx, err := db.BeginTx(ctx, nil)
	if err != nil {
		return nil, status.Errorf(codes.Internal, "begin tx: %v", err)
	}
	defer tx.Rollback()

	// Invalidate old refresh tokens for this user
	_, err = tx.ExecContext(ctx, `
			DELETE FROM refresh_tokens 
			WHERE user_id = $1`,
		userID)

	if err != nil {
		return nil, status.Errorf(codes.Internal, "invalidate old tokens: %v", err)
	}

	// Store new refresh token
	refreshExpiresAt := time.Now().Add(refreshTokenExp)
	_, err = tx.ExecContext(ctx, `
			INSERT INTO refresh_tokens (id, user_id, token, expires_at)
			VALUES (gen_random_uuid(), $1, $2, $3)`,
		userID, refreshToken, refreshExpiresAt)

	if err != nil {
		return nil, status.Errorf(codes.Internal, "store refresh token: %v", err)
	}

	// Get user permissions
	permissions, err := getUserPermissions(ctx, tx, userID)
	if err != nil {
		// Use default permissions if we can't fetch
		permissions = &permissionspb.PermissionSet{}
	}

	if err := tx.Commit(); err != nil {
		return nil, status.Errorf(codes.Internal, "commit: %v", err)
	}

	// Create user profile
	profile := &userpb.User{
		Id:          userID.String(),
		Username:    username,
		DisplayName: displayName,
	}
	if avatarUrl.Valid {
		profile.AvatarUrl = avatarUrl.String
	}

	return &authpb.AuthSession{
		Tokens: &authpb.TokenPair{
			AccessToken:  accessToken,
			RefreshToken: refreshToken,
		},
		Iat:            uint64(time.Now().Unix()),
		Exp:            uint64(time.Now().Add(accessTokenExp).Unix()),
		IsChatMember:   isChatMember,
		JoinRequestUrl: "https://t.me/your_musicclub_bot?start=join", // TODO start link generation
		Profile:        profile,
		Permissions:    permissions,
	}, nil
}

func (s *AuthService) Refresh(ctx context.Context, req *authpb.RefreshRequest) (*authpb.TokenPair, error) {
	db, err := dbFromCtx(ctx)
	if err != nil {
		return nil, status.Error(codes.Internal, err.Error())
	}

	refreshToken := req.GetRefreshToken()
	if refreshToken == "" {
		return nil, status.Error(codes.InvalidArgument, "refresh token is required")
	}

	// Verify refresh token exists and is valid
	var userID uuid.UUID
	var expiresAt time.Time

	err = db.QueryRowContext(ctx, `
		SELECT user_id, expires_at 
		FROM refresh_tokens 
		WHERE token = $1 AND expires_at > NOW()`,
		refreshToken,
	).Scan(&userID, &expiresAt)

	if err != nil {
		if err == sql.ErrNoRows {
			return nil, status.Error(codes.Unauthenticated, "invalid or expired refresh token")
		}
		return nil, status.Errorf(codes.Internal, "query refresh token: %v", err)
	}

	// Get user info for new token
	var username string
	err = db.QueryRowContext(ctx, `
		SELECT username FROM app_user WHERE id = $1`,
		userID,
	).Scan(&username)

	if err != nil {
		return nil, status.Errorf(codes.Internal, "query user: %v", err)
	}

	// Generate new tokens
	newAccessToken, err := generateAccessToken(ctx, userID, username)
	if err != nil {
		return nil, status.Errorf(codes.Internal, "generate access token: %v", err)
	}

	newRefreshToken, err := generateRefreshToken()
	if err != nil {
		return nil, status.Errorf(codes.Internal, "generate refresh token: %v", err)
	}

	// Update refresh token in database
	tx, err := db.BeginTx(ctx, nil)
	if err != nil {
		return nil, status.Errorf(codes.Internal, "begin tx: %v", err)
	}
	defer tx.Rollback()

	// Delete old refresh token
	_, err = tx.ExecContext(ctx, `
		DELETE FROM refresh_tokens WHERE token = $1`,
		refreshToken)

	if err != nil {
		return nil, status.Errorf(codes.Internal, "delete old token: %v", err)
	}

	// Store new refresh token
	newRefreshExpiresAt := time.Now().Add(refreshTokenExp)
	_, err = tx.ExecContext(ctx, `
		INSERT INTO refresh_tokens (id, user_id, token, expires_at)
		VALUES (gen_random_uuid(), $1, $2, $3)`,
		userID, newRefreshToken, newRefreshExpiresAt)

	if err != nil {
		return nil, status.Errorf(codes.Internal, "store new token: %v", err)
	}

	if err := tx.Commit(); err != nil {
		return nil, status.Errorf(codes.Internal, "commit: %v", err)
	}

	return &authpb.TokenPair{
		AccessToken:  newAccessToken,
		RefreshToken: newRefreshToken,
	}, nil
}

func (s *AuthService) GetTgLoginLink(ctx context.Context, req *userpb.User) (*authpb.TgLoginLinkResponse, error) {
	// Get user ID from context (user must be authenticated)
	userIDStr, err := userIDFromCtx(ctx)
	if err != nil {
		return nil, status.Error(codes.Unauthenticated, "authentication required")
	}

	userID, err := uuid.Parse(userIDStr)
	if err != nil {
		return nil, status.Error(codes.InvalidArgument, "invalid user ID")
	}

	db, err := dbFromCtx(ctx)
	if err != nil {
		return nil, status.Error(codes.Internal, err.Error())
	}

	// Check if user already has Telegram linked
	var existingTgID sql.NullInt64
	err = db.QueryRowContext(ctx, `
		SELECT tg_user_id FROM app_user WHERE id = $1`,
		userID,
	).Scan(&existingTgID)

	if err != nil {
		return nil, status.Errorf(codes.Internal, "query user: %v", err)
	}

	if existingTgID.Valid {
		return nil, status.Error(codes.AlreadyExists, "Telegram already linked to this account")
	}

	// Generate a unique login token
	loginToken, err := generateRefreshToken()
	if err != nil {
		return nil, status.Errorf(codes.Internal, "generate login token: %v", err)
	}

	// Store the login token in tg_auth_session table
	// Note: You need to create tg_auth_session table as referenced in your schema
	_, err = db.ExecContext(ctx, `
		INSERT INTO tg_auth_session (id, user_id, tg_user_id, success, created_at)
		VALUES (gen_random_uuid(), $1, NULL, FALSE, NOW())`,
		userID,
	)

	if err != nil {
		// Table might not exist, create it
		if strings.Contains(err.Error(), "does not exist") {
			_, createErr := db.ExecContext(ctx, `
				CREATE TABLE IF NOT EXISTS tg_auth_session (
					id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
					user_id UUID NOT NULL REFERENCES app_user(id) ON DELETE CASCADE,
					tg_user_id BIGINT,
					success BOOLEAN NOT NULL DEFAULT FALSE,
					created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
				);
				CREATE UNIQUE INDEX IF NOT EXISTS idx_tg_auth_session_user ON tg_auth_session (user_id);
			`)
			if createErr != nil {
				return nil, status.Errorf(codes.Internal, "create tg_auth_session table: %v", createErr)
			}

			// Retry insert
			_, err = db.ExecContext(ctx, `
				INSERT INTO tg_auth_session (user_id, success)
				VALUES ($1, FALSE)`,
				userID,
			)
			if err != nil {
				return nil, status.Errorf(codes.Internal, "store tg auth session: %v", err)
			}
		} else {
			return nil, status.Errorf(codes.Internal, "store tg auth session: %v", err)
		}
	}

	// Generate Telegram bot deep link
	botUsername := "your_musicclub_bot" // Replace with your bot username
	loginLink := fmt.Sprintf("https://t.me/%s?start=auth_%s", botUsername, loginToken)

	return &authpb.TgLoginLinkResponse{
		LoginLink: loginLink,
	}, nil
}

func (s *AuthService) GetProfile(ctx context.Context, req *emptypb.Empty) (*authpb.ProfileResponse, error) {
	// Extract user ID from context
	userIDStr, err := userIDFromCtx(ctx)
	if err != nil {
		return nil, status.Error(codes.Unauthenticated, "authentication required")
	}

	userID, err := uuid.Parse(userIDStr)
	if err != nil {
		return nil, status.Error(codes.Internal, "invalid user ID format")
	}

	db, err := dbFromCtx(ctx)
	if err != nil {
		return nil, status.Error(codes.Internal, err.Error())
	}

	// Get user profile
	var username, displayName string
	var avatarUrl sql.NullString
	var tgUserID sql.NullInt64
	var isChatMember bool
	var createdAt time.Time

	err = db.QueryRowContext(ctx, `
		SELECT username, display_name, avatar_url, tg_user_id, is_chat_member, created_at
		FROM app_user 
		WHERE id = $1`,
		userID,
	).Scan(&username, &displayName, &avatarUrl, &tgUserID, &isChatMember, &createdAt)

	if err != nil {
		if err == sql.ErrNoRows {
			return nil, status.Error(codes.NotFound, "user not found")
		}
		return nil, status.Errorf(codes.Internal, "query user: %v", err)
	}

	// Get user permissions
	permissions, err := getUserPermissions(ctx, db, userID)
	if err != nil {
		// Use default permissions if we can't fetch
		permissions = &permissionspb.PermissionSet{}
	}

	profile := &userpb.User{
		Id:          userID.String(),
		Username:    username,
		DisplayName: displayName,
	}
	if avatarUrl.Valid {
		profile.AvatarUrl = avatarUrl.String
	}
	if tgUserID.Valid {
		profile.TelgramId = uint64(tgUserID.Int64)
	}

	return &authpb.ProfileResponse{
		Profile:     profile,
		Permissions: permissions,
	}, nil
}

// Helper functions
func acceptablePassword(password string) bool {
	if password == "" {
		return false
	}
	if len(password) < 8 {
		return false
	}
	// Add more complexity checks if needed
	// e.g., require at least one uppercase, one lowercase, one number, one special char
	return true
}

func getUserPermissions(ctx context.Context, db interface{}, userID uuid.UUID) (*permissionspb.PermissionSet, error) {
	var queryRow interface {
		QueryRowContext(ctx context.Context, query string, args ...interface{}) *sql.Row
	}

	switch d := db.(type) {
	case *sql.DB:
		queryRow = d
	case *sql.Tx:
		queryRow = d
	default:
		return nil, fmt.Errorf("unsupported database type")
	}

	permissions := &permissionspb.PermissionSet{
		Join:   &permissionspb.JoinPermissions{},
		Songs:  &permissionspb.SongPermissions{},
		Events: &permissionspb.EventPermissions{},
	}

	err := queryRow.QueryRowContext(ctx, `
    SELECT edit_own_participation, edit_any_participation, 
           edit_own_songs, edit_any_songs, edit_events, edit_tracklists
    FROM user_permissions 
    WHERE user_id = $1`,
		userID,
	).Scan(
		&permissions.Join.EditOwnParticipation,
		&permissions.Join.EditAnyParticipation,
		&permissions.Songs.EditOwnSongs,
		&permissions.Songs.EditAnySongs,
		&permissions.Events.EditEvents,
		&permissions.Events.EditTracklists,
	)

	if err != nil {
		if err == sql.ErrNoRows {
			// Return default permissions if user has no specific permissions
			return &permissionspb.PermissionSet{}, nil
		}
		return nil, err
	}

	return permissions, nil
}

var PublicMethods = map[string]bool{
	"/musicclub.auth.AuthService/Login":    true,
	"/musicclub.auth.AuthService/Register": true,
	"/musicclub.auth.AuthService/Refresh":  true,
}

// Authentication middleware
func AuthInterceptor(ctx context.Context, req interface{}, info *grpc.UnaryServerInfo, handler grpc.UnaryHandler) (interface{}, error) {
	if PublicMethods[info.FullMethod] {
		return handler(ctx, req)
	}

	md, ok := metadata.FromIncomingContext(ctx)
	if !ok {
		return nil, status.Error(codes.Unauthenticated, "missing metadata")
	}

	authHeaders := md.Get("authorization")
	if len(authHeaders) == 0 {
		return nil, status.Error(codes.Unauthenticated, "missing authorization header")
	}

	tokenString := authHeaders[0]
	if !strings.HasPrefix(tokenString, "Bearer ") {
		return nil, status.Error(codes.Unauthenticated, "invalid authorization format")
	}

	tokenString = strings.TrimPrefix(tokenString, "Bearer ")

	claims, err := verifyToken(ctx, tokenString)
	if err != nil {
		return nil, status.Error(codes.Unauthenticated, "invalid token")
	}

	db, err := dbFromCtx(ctx)
	if err == nil {
		var exists bool
		userID, parseErr := uuid.Parse(claims.UserID)
		if parseErr == nil {
			err = db.QueryRowContext(ctx,
				`SELECT EXISTS(SELECT 1 FROM app_user WHERE id = $1)`,
				userID,
			).Scan(&exists)

			if err == nil && !exists {
				return nil, status.Error(codes.Unauthenticated, "user no longer exists")
			}
		}
	}

	ctx = context.WithValue(ctx, "user_claims", claims)
	ctx = context.WithValue(ctx, "user_id", claims.UserID)

	return handler(ctx, req)
}
