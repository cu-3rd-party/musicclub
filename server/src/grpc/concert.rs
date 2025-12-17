use api::pb::concert_service_server::ConcertService;
use api::pb::{
    Concert, CreateConcertRequest, DeleteConcertRequest, GetConcertRequest, ListConcertsRequest,
    ListConcertsResponse, UpdateConcertRequest,
};
use tonic::{Request, Response, Result, Status};

#[derive(Debug, Default)]
pub struct ConcertServer;

#[tonic::async_trait]
impl ConcertService for ConcertServer {
    async fn create_concert(
        &self,
        _request: Request<CreateConcertRequest>,
    ) -> Result<Response<Concert>, Status> {
        todo!()
    }

    async fn get_concert(
        &self,
        _request: Request<GetConcertRequest>,
    ) -> Result<Response<Concert>, Status> {
        todo!()
    }

    async fn list_concerts(
        &self,
        _request: Request<ListConcertsRequest>,
    ) -> Result<Response<ListConcertsResponse>, Status> {
        todo!()
    }

    async fn update_concert(
        &self,
        _request: Request<UpdateConcertRequest>,
    ) -> Result<Response<Concert>, Status> {
        todo!()
    }

    async fn delete_concert(
        &self,
        _request: Request<DeleteConcertRequest>,
    ) -> Result<Response<()>, Status> {
        todo!()
    }
}
