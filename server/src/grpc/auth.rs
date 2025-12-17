use api::pb::{LoginResponse, TgLogin, auth_service_server::AuthService};
use tonic::{Request, Response, Result, Status};

#[derive(Debug, Default)]
pub struct AuthServer;

#[tonic::async_trait]
impl AuthService for AuthServer {
    async fn login_tg(
        &self,
        _request: Request<TgLogin>,
    ) -> Result<Response<LoginResponse>, Status> {
        todo!()
    }
}
