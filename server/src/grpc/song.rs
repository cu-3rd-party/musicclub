use api::pb::song_service_server::SongService;
use api::pb::{
    CreateSongRequest, DeleteSongRequest, GetSongRequest, ListSongsRequest, ListSongsResponse,
    Song, UpdateSongRequest,
};
use tonic::{Request, Response, Result, Status};

#[derive(Debug, Default)]
pub struct SongServer;

#[tonic::async_trait]
impl SongService for SongServer {
    async fn create_song(
        &self,
        _request: Request<CreateSongRequest>,
    ) -> Result<Response<Song>, Status> {
        todo!()
    }

    async fn get_song(&self, _request: Request<GetSongRequest>) -> Result<Response<Song>, Status> {
        todo!()
    }

    async fn list_songs(
        &self,
        _request: Request<ListSongsRequest>,
    ) -> Result<Response<ListSongsResponse>, Status> {
        todo!()
    }

    async fn update_song(
        &self,
        _request: Request<UpdateSongRequest>,
    ) -> Result<Response<Song>, Status> {
        todo!()
    }

    async fn delete_song(
        &self,
        _request: Request<DeleteSongRequest>,
    ) -> Result<Response<()>, Status> {
        todo!()
    }
}
