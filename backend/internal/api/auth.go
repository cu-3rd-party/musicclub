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
	"golang.org/x/crypto/bcrypt"
	"google.golang.org/grpc"
	"google.golang.org/grpc/codes"
	"google.golang.org/grpc/metadata"
	"google.golang.org/grpc/status"
	"google.golang.org/protobuf/types/known/timestamppb"

	authpb "musicclubbot/backend/proto"
	permissionspb "musicclubbot/backend/proto"
	userpb "musicclubbot/backend/proto"

	emptypb "google.golang.org/protobuf/types/known/emptypb"
)

// JWT configuration
const (
	jwtSecretKey     = "your-secret-key-change-in-production" // Change this in production
	accessTokenExp   = 15 * time.Minute                       // 15 minutes
	refreshTokenExp  = 7 * 24 * time.Hour                     // 7 days
	refreshTokenSize = 32                                     // bytes for refresh token
)

type JWTClaims struct {
	UserID   int64  `json:"user_id"`
	Username string `json:"username"`
	jwt.RegisteredClaims
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

func generateAccessToken(userID int64, username string) (string, error) {
	expirationTime := time.Now().Add(accessTokenExp)

	claims := &JWTClaims{
		UserID:   userID,
		Username: username,
		RegisteredClaims: jwt.RegisteredClaims{
			ExpiresAt: jwt.NewNumericDate(expirationTime),
			IssuedAt:  jwt.NewNumericDate(time.Now()),
			Issuer:    "musicclubbot",
			Subject:   fmt.Sprintf("%d", userID),
		},
	}

	token := jwt.NewWithClaims(jwt.SigningMethodHS256, claims)
	return token.SignedString([]byte(jwtSecretKey))
}

func generateRefreshToken() (string, error) {
	// Generate a secure random string for refresh token
	tokenBytes := make([]byte, refreshTokenSize)
	if _, err := rand.Read(tokenBytes); err != nil {
		return "", err
	}
	return base64.URLEncoding.EncodeToString(tokenBytes), nil
}

func verifyToken(tokenString string) (*JWTClaims, error) {
	token, err := jwt.ParseWithClaims(tokenString, &JWTClaims{}, func(token *jwt.Token) (interface{}, error) {
		if _, ok := token.Method.(*jwt.SigningMethodHMAC); !ok {
			return nil, fmt.Errorf("unexpected signing method: %v", token.Header["alg"])
		}
		return []byte(jwtSecretKey), nil
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
}

func (s *AuthService) Register(ctx context.Context, req *authpb.RegisterUserRequest) (*authpb.AuthSession, error) {
	db, err := dbFromCtx(ctx)
	if err != nil {
		return nil, status.Error(codes.Internal, err.Error())
	}

	username := req.GetCredentials().Username
	if username == "" {
		return nil, status.Error(codes.InvalidArgument, "username is required")
	}

	var exists bool
	err = db.QueryRowContext(ctx, `SELECT EXISTS(SELECT 1 FROM "app_user" WHERE username=$1)`, username).Scan(&exists)
	if err != nil {
		return nil, status.Errorf(codes.Internal, "check existing username: %v", err)
	}
	if exists {
		return nil, status.Error(codes.AlreadyExists, "username already taken")
	}

	password := req.GetCredentials().Password
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

	var userID int64
	err = tx.QueryRowContext(ctx, `
		INSERT INTO "app_user" (username, password_hash, display_name, avatar_url) 
		VALUES ($1, $2, $3, $4)
		RETURNING id`,
		username,
		hashedPassword,
		req.GetProfile().GetDisplayName(),
		req.GetProfile().GetAvatarUrl(),
	).Scan(&userID)

	if err != nil {
		return nil, status.Errorf(codes.Internal, "insert user: %v", err)
	}

	// Generate JWT tokens
	accessToken, err := generateAccessToken(userID, username)
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
		INSERT INTO refresh_tokens (user_id, token, expires_at)
		VALUES ($1, $2, $3)`,
		userID, refreshToken, refreshExpiresAt)

	if err != nil {
		return nil, status.Errorf(codes.Internal, "store refresh token: %v", err)
	}

	permissions := &permissionspb.PermissionSet{} // all false

	if err := tx.Commit(); err != nil {
		return nil, status.Errorf(codes.Internal, "commit: %v", err)
	}

	// Create user profile response
	profile := &userpb.User{
		Id:          userID,
		Username:    username,
		DisplayName: req.GetProfile().GetDisplayName(),
		AvatarUrl:   req.GetProfile().GetAvatarUrl(),
		CreatedAt:   timestamppb.New(time.Now()),
	}

	return &authpb.AuthSession{
		Tokens: &authpb.TokenPair{
			AccessToken:  accessToken,
			RefreshToken: refreshToken,
		},
		Iat:            uint64(time.Now().Unix()),
		Exp:            uint64(time.Now().Add(accessTokenExp).Unix()),
		IsChatMember:   false,                                      // New user not in chat yet
		JoinRequestUrl: "https://t.me/your_bot?start=join_request", // Your bot join link
		Profile:        profile,
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
	var userID int64
	var hashedPassword string
	var displayName, avatarUrl string
	var createdAt time.Time

	err = db.QueryRowContext(ctx, `
		SELECT id, password_hash, display_name, avatar_url, created_at
		FROM "app_user" 
		WHERE username = $1`,
		username,
	).Scan(&userID, &hashedPassword, &displayName, &avatarUrl, &createdAt)

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
	accessToken, err := generateAccessToken(userID, username)
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
		INSERT INTO refresh_tokens (user_id, token, expires_at)
		VALUES ($1, $2, $3)`,
		userID, refreshToken, refreshExpiresAt)

	if err != nil {
		return nil, status.Errorf(codes.Internal, "store refresh token: %v", err)
	}

	// Check if user is chat member (you need to implement this based on your chat membership logic)
	var isChatMember bool
	err = tx.QueryRowContext(ctx, `
		SELECT EXISTS(
			SELECT 1 FROM chat_members 
			WHERE user_id = $1 AND is_active = true
		)`, userID).Scan(&isChatMember)

	if err != nil && err != sql.ErrNoRows {
		// Log but don't fail if we can't check membership
		isChatMember = false
	}

	// Get user permissions (implement based on your permission system)
	permissions, err := getUserPermissions(ctx, tx, userID)
	if err != nil {
		// Use default permissions if we can't fetch
		permissions = &permissionspb.PermissionSet{
			CanViewContent:  true,
			CanJoinChat:     !isChatMember,
			CanPostContent:  false,
			CanInviteUsers:  false,
			CanManageUsers:  false,
			CanModerateChat: false,
		}
	}

	if err := tx.Commit(); err != nil {
		return nil, status.Errorf(codes.Internal, "commit: %v", err)
	}

	// Create user profile
	profile := &userpb.User{
		Id:          userID,
		Username:    username,
		DisplayName: displayName,
		AvatarUrl:   avatarUrl,
		CreatedAt:   timestamppb.New(createdAt),
	}

	return &authpb.AuthSession{
		Tokens: &authpb.TokenPair{
			AccessToken:  accessToken,
			RefreshToken: refreshToken,
		},
		Iat:            uint64(time.Now().Unix()),
		Exp:            uint64(time.Now().Add(accessTokenExp).Unix()),
		IsChatMember:   isChatMember,
		JoinRequestUrl: "https://t.me/your_bot?start=join_request",
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
	var userID int64
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
		SELECT username FROM "app_user" WHERE id = $1`,
		userID,
	).Scan(&username)

	if err != nil {
		return nil, status.Errorf(codes.Internal, "query user: %v", err)
	}

	// Generate new tokens
	newAccessToken, err := generateAccessToken(userID, username)
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
		INSERT INTO refresh_tokens (user_id, token, expires_at)
		VALUES ($1, $2, $3)`,
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
	// Generate Telegram bot login link with user ID as parameter
	// This would typically be a deep link to your Telegram bot
	botUsername := "your_bot_username" // Replace with your bot username

	// Generate a unique login token for this request
	loginToken, err := generateRefreshToken()
	if err != nil {
		return nil, status.Errorf(codes.Internal, "generate login token: %v", err)
	}

	// Store the login token temporarily (you might want to use Redis for this)
	// For now, we'll just return a static link
	loginLink := fmt.Sprintf("https://t.me/%s?start=login_%s", botUsername, loginToken)

	return &authpb.TgLoginLinkResponse{
		LoginLink: loginLink,
	}, nil
}

func (s *AuthService) GetProfile(ctx context.Context, req *emptypb.Empty) (*authpb.ProfileResponse, error) {
	// Extract user ID from context (set by authentication middleware)
	userID, err := getUserIDFromCtx(ctx)
	if err != nil {
		return nil, status.Error(codes.Unauthenticated, "authentication required")
	}

	db, err := dbFromCtx(ctx)
	if err != nil {
		return nil, status.Error(codes.Internal, err.Error())
	}

	// Get user profile
	var username, displayName, avatarUrl string
	var createdAt time.Time

	err = db.QueryRowContext(ctx, `
		SELECT username, display_name, avatar_url, created_at
		FROM "app_user" 
		WHERE id = $1`,
		userID,
	).Scan(&username, &displayName, &avatarUrl, &createdAt)

	if err != nil {
		if err == sql.ErrNoRows {
			return nil, status.Error(codes.NotFound, "user not found")
		}
		return nil, status.Errorf(codes.Internal, "query user: %v", err)
	}

	// Check if user is chat member
	var isChatMember bool
	err = db.QueryRowContext(ctx, `
		SELECT EXISTS(
			SELECT 1 FROM chat_members 
			WHERE user_id = $1 AND is_active = true
		)`, userID).Scan(&isChatMember)

	if err != nil && err != sql.ErrNoRows {
		isChatMember = false
	}

	// Get user permissions
	permissions, err := getUserPermissions(ctx, db, userID)
	if err != nil {
		// Use default permissions if we can't fetch
		permissions = &permissionspb.PermissionSet{
			CanViewContent:  true,
			CanJoinChat:     !isChatMember,
			CanPostContent:  false,
			CanInviteUsers:  false,
			CanManageUsers:  false,
			CanModerateChat: false,
		}
	}

	profile := &userpb.User{
		Id:          userID,
		Username:    username,
		DisplayName: displayName,
		AvatarUrl:   avatarUrl,
		CreatedAt:   timestamppb.New(createdAt),
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
	return true
}

func getUserIDFromCtx(ctx context.Context) (int64, error) {
	// This would be set by your authentication middleware
	// For example, if you use JWT middleware that adds claims to context
	claims, ok := ctx.Value("user_claims").(*JWTClaims)
	if !ok || claims == nil {
		return 0, fmt.Errorf("no user claims in context")
	}
	return claims.UserID, nil
}

func getUserPermissions(ctx context.Context, db interface{}, userID int64) (*permissionspb.PermissionSet, error) {
	// Implement your permission logic here
	// This is a simplified example - adjust based on your actual permission system

	// For now, return default permissions
	// You might want to query a permissions table or check user roles
	return &permissionspb.PermissionSet{
		CanViewContent:  true,
		CanJoinChat:     true,
		CanPostContent:  true,
		CanInviteUsers:  false,
		CanManageUsers:  false,
		CanModerateChat: false,
	}, nil
}

// Middleware for JWT authentication
func AuthInterceptor(ctx context.Context, req interface{}, info *grpc.UnaryServerInfo, handler grpc.UnaryHandler) (interface{}, error) {
	// Skip authentication for login and register endpoints
	if info.FullMethod == "/musicclub.auth.AuthService/Login" ||
		info.FullMethod == "/musicclub.auth.AuthService/Register" ||
		info.FullMethod == "/musicclub.auth.AuthService/Refresh" {
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

	claims, err := verifyToken(tokenString)
	if err != nil {
		return nil, status.Error(codes.Unauthenticated, "invalid token")
	}

	// Add claims to context for use in handlers
	ctx = context.WithValue(ctx, "user_claims", claims)

	return handler(ctx, req)
}
