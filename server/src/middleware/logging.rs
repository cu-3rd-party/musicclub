use std::time::Instant;

use tonic::body::Body;
use tonic::codegen::http::Request;
use tonic::codegen::http::Response;
use tonic_middleware::Middleware;
use tonic_middleware::ServiceBound;

#[derive(Debug, Default, Clone)]
pub struct ConsoleLoggingMiddleware;

#[tonic::async_trait]
impl<S> Middleware<S> for ConsoleLoggingMiddleware
where
    S: ServiceBound,
    S::Future: Send,
{
    async fn call(&self, req: Request<Body>, mut service: S) -> Result<Response<Body>, S::Error> {
        let start_time = Instant::now();
        let remote_addr = req.uri().path().to_string().clone();

        let result = service.call(req).await?;

        let elapsed_time = start_time.elapsed();

        log::info!("{} completed in {:?}", remote_addr, elapsed_time);

        Ok(result)
    }
}
