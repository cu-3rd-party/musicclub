use std::collections::HashSet;

use tonic::body::Body;
use tonic::codegen::http::Request;
use tonic::codegen::http::Response;
use tonic_middleware::Middleware;
use tonic_middleware::ServiceBound;

#[derive(Clone, Debug)]
pub struct AdminOnlyMiddleware {
    admin_ids: HashSet<u64>,
}

impl AdminOnlyMiddleware {
    pub fn new(admin_ids: HashSet<u64>) -> Self {
        Self { admin_ids }
    }
}

#[tonic::async_trait]
impl<S> Middleware<S> for AdminOnlyMiddleware
where
    S: ServiceBound,
    S::Future: Send,
{
    async fn call(&self, req: Request<Body>, mut service: S) -> Result<Response<Body>, S::Error> {
        if req.uri().path().ends_with("/CreateConcert") {
            println!("{:?}", req.headers());
            let user_id = req
                .headers()
                .get("x-user-id")
                .and_then(|value| value.to_str().ok())
                .and_then(|value| value.parse::<u64>().ok());
            println!("{:?}", user_id);

            if user_id.is_none() || !self.admin_ids.contains(&user_id.expect("checked")) {
                let response = tonic::Status::permission_denied("admin required").into_http();
                return Ok(response);
            }
        }

        service.call(req).await
    }
}
