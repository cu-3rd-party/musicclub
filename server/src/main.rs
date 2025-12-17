mod grpc;

use api::pb::{auth_service_server, concert_service_server, song_service_server};
use env_logger::Env;
use tonic::{Result, transport::Server};

use crate::grpc::{auth::AuthServer, concert::ConcertServer, song::SongServer};

#[tokio::main]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    env_logger::Builder::from_env(Env::default().default_filter_or("debug")).init();
    let addr = "[::1]:6969".parse()?;

    log::info!("Server is running at {addr}");
    Server::builder()
        .add_service(auth_service_server::AuthServiceServer::new(
            AuthServer::default(),
        ))
        .add_service(song_service_server::SongServiceServer::new(
            SongServer::default(),
        ))
        .add_service(concert_service_server::ConcertServiceServer::new(
            ConcertServer::default(),
        ))
        .serve(addr)
        .await?;

    Ok(())
}
