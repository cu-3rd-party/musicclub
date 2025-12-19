use std::collections::HashSet;
use std::sync::Arc;
use std::time::{Duration, SystemTime, UNIX_EPOCH};

use api::pb::{LoginResponse, TgLogin, auth_service_server::AuthService};
use jsonwebtoken::{Algorithm, DecodingKey, EncodingKey, Header, Validation, decode, encode};
use serde::{Deserialize, Serialize};
use tonic::body::Body;
use tonic::codegen::http::Request as HttpRequest;
use tonic::{Request, Response, Result, Status};
use tonic_middleware::RequestInterceptor;

#[derive(Debug, Serialize, Deserialize)]
pub struct Claims {
    sub: u64,
    exp: usize,
    iat: usize,
    is_admin: bool,
}

#[derive(Debug)]
struct Keys {
    encoding: EncodingKey,
    decoding: DecodingKey,
}

impl Keys {
    fn new(secret: &[u8]) -> Self {
        Self {
            encoding: EncodingKey::from_secret(secret),
            decoding: DecodingKey::from_secret(secret),
        }
    }
}

#[derive(Debug)]
pub struct AuthServer {
    keys: Keys,
    admin_ids: Arc<HashSet<u64>>,
    ttl: Duration,
}

impl AuthServer {
    pub fn new(secret_key: &[u8], admin_ids: HashSet<u64>, ttl: Duration) -> Self {
        Self {
            keys: Keys::new(secret_key),
            admin_ids: Arc::new(admin_ids),
            ttl,
        }
    }

    fn now_ts() -> usize {
        SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .expect("time went backwards")
            .as_secs() as usize
    }

    fn generate_claims(&self, payload: u64) -> Claims {
        let now = Self::now_ts();
        Claims {
            sub: payload,
            iat: now,
            exp: now + self.ttl.as_secs() as usize,
            is_admin: self.admin_ids.contains(&payload),
        }
    }

    pub fn sign(&self, claims: &Claims) -> String {
        encode(&Header::default(), claims, &self.keys.encoding).expect("jwt encode failed")
    }
}

#[tonic::async_trait]
impl AuthService for AuthServer {
    async fn login_tg(&self, request: Request<TgLogin>) -> Result<Response<LoginResponse>, Status> {
        let tg_id = request.into_inner().tg_id;
        if tg_id == 0 {
            return Err(Status::invalid_argument("tg_id must be non-zero"));
        }
        let claims = self.generate_claims(tg_id);
        let token = self.sign(&claims);
        log::debug!("Handled login request for {tg_id} and returned token {token:.20}");
        Ok(Response::new(LoginResponse {
            token: token,
            is_admin: claims.is_admin,
            iat: claims.iat as u64,
            exp: claims.exp as u64,
        }))
    }
}

#[derive(Clone, Debug)]
pub struct AuthInterceptor {
    keys: Arc<Keys>,
}

impl AuthInterceptor {
    pub fn new(secret_key: &[u8]) -> Self {
        Self {
            keys: Arc::new(Keys::new(secret_key)),
        }
    }

    fn decode(&self, token: &str) -> Result<Claims, Status> {
        let mut validation = Validation::new(Algorithm::HS256);
        validation.validate_exp = true;
        let data = decode::<Claims>(token, &self.keys.decoding, &validation)
            .map_err(|_| Status::unauthenticated("invalid token"))?;
        Ok(data.claims)
    }
}

#[tonic::async_trait]
impl RequestInterceptor for AuthInterceptor {
    async fn intercept(&self, req: HttpRequest<Body>) -> Result<HttpRequest<Body>, Status> {
        if !req.uri().path().ends_with("/CreateConcert") {
            return Ok(req);
        }

        let auth_header = req
            .headers()
            .get("authorization")
            .and_then(|value| value.to_str().ok())
            .ok_or_else(|| Status::unauthenticated("authorization header required"))?;
        let token = auth_header.strip_prefix("Bearer ").unwrap_or(auth_header);
        let claims = self.decode(token)?;

        let mut req = req;
        let header_value = tonic::codegen::http::HeaderValue::from_str(&claims.sub.to_string())
            .map_err(|_| Status::internal("invalid user id header"))?;
        req.headers_mut().insert("x-user-id", header_value);
        Ok(req)
    }
}

#[cfg(test)]
mod tests {
    use super::AuthServer;
    use api::pb::TgLogin;
    use api::pb::auth_service_client::AuthServiceClient;
    use api::pb::auth_service_server::AuthService;
    use api::pb::auth_service_server::AuthServiceServer;
    use jsonwebtoken::{DecodingKey, Validation, decode};
    use std::collections::HashSet;
    use std::net::SocketAddr;
    use std::time::Duration;
    use tokio_stream::wrappers::TcpListenerStream;
    use tonic::{Request, transport::Channel, transport::Server};

    #[tokio::test]
    async fn login_tg_returns_jwt() {
        let mut admins = HashSet::new();
        admins.insert(7_u64);
        let server = AuthServer::new(b"secret", admins, Duration::from_secs(3600));
        let response = server
            .login_tg(Request::new(TgLogin { tg_id: 7 }))
            .await
            .expect("response");

        let token = &response.get_ref().token;
        let decoded = decode::<super::Claims>(
            token,
            &DecodingKey::from_secret(b"secret"),
            &Validation::default(),
        )
        .expect("decoded");

        assert_eq!(decoded.claims.sub, 7);
        assert!(decoded.claims.is_admin);
    }

    async fn start_server(server: AuthServer) -> Option<(SocketAddr, tokio::task::JoinHandle<()>)> {
        let addr: SocketAddr = "127.0.0.1:0".parse().expect("addr");
        let listener = match tokio::net::TcpListener::bind(&addr).await {
            Ok(listener) => listener,
            Err(err) if err.kind() == std::io::ErrorKind::PermissionDenied => return None,
            Err(err) => panic!("bind failed: {err}"),
        };
        let addr = listener.local_addr().expect("local addr");

        let handle = tokio::spawn(async move {
            Server::builder()
                .add_service(AuthServiceServer::new(server))
                .serve_with_incoming(TcpListenerStream::new(listener))
                .await
                .expect("grpc server failed");
        });

        tokio::time::sleep(tokio::time::Duration::from_millis(50)).await;
        Some((addr, handle))
    }

    async fn create_client(addr: SocketAddr) -> AuthServiceClient<Channel> {
        let endpoint = format!("http://{}:{}", addr.ip(), addr.port());
        AuthServiceClient::connect(endpoint).await.expect("connect")
    }

    #[tokio::test]
    async fn e2e_auth_login() {
        let admins = HashSet::new();
        let server = AuthServer::new(b"secret", admins, Duration::from_secs(3600));
        let Some((addr, _handle)) = start_server(server).await else {
            eprintln!("skipping e2e_auth_login: tcp bind not permitted");
            return;
        };
        let mut client = create_client(addr).await;

        let response = client
            .login_tg(Request::new(TgLogin { tg_id: 11 }))
            .await
            .expect("login")
            .into_inner();
        assert!(!response.token.is_empty());
    }
}
