use std::sync::Arc;

use api::pb::song_service_server::SongService;
use api::pb::{
    CreateSongRequest, DeleteSongRequest, GetSongRequest, ListSongsRequest, ListSongsResponse,
    Song, UpdateSongRequest,
};
use async_trait::async_trait;
use sqlx::FromRow;
use sqlx::PgPool;
use tonic::{Request, Response, Result, Status};

#[derive(Clone)]
pub struct SongServer {
    store: Arc<dyn SongStore>,
}

#[derive(Clone, Debug, FromRow)]
struct SongRow {
    id: i32,
    title: String,
    description: Option<String>,
    link: Option<String>,
}

#[derive(Clone, Debug)]
pub struct SongRecord {
    pub id: u64,
    pub title: String,
    pub description: Option<String>,
    pub link: Option<String>,
}

#[derive(Debug)]
pub enum StoreError {
    NotFound,
    Database(String),
}

#[async_trait]
pub trait SongStore: Send + Sync {
    async fn create(&self, song: SongRecord) -> Result<SongRecord, StoreError>;
    async fn get(&self, id: u64) -> Result<SongRecord, StoreError>;
    async fn list(&self, limit: i64) -> Result<Vec<SongRecord>, StoreError>;
    async fn update(&self, song: SongRecord) -> Result<SongRecord, StoreError>;
    async fn delete(&self, id: u64) -> Result<(), StoreError>;
}

#[derive(Debug)]
struct PostgresSongStore {
    pool: PgPool,
}

impl PostgresSongStore {
    fn new(pool: PgPool) -> Self {
        Self { pool }
    }
}

#[async_trait]
impl SongStore for PostgresSongStore {
    async fn create(&self, song: SongRecord) -> Result<SongRecord, StoreError> {
        let row = sqlx::query_as::<_, SongRow>(
            r#"
            INSERT INTO songs (title, description, link)
            VALUES ($1, $2, $3)
            RETURNING id, title, description, link
            "#,
        )
        .bind(song.title)
        .bind(song.description)
        .bind(song.link)
        .fetch_one(&self.pool)
        .await
        .map_err(|err| StoreError::Database(err.to_string()))?;

        Ok(song_from_row(row))
    }

    async fn get(&self, id: u64) -> Result<SongRecord, StoreError> {
        let row = sqlx::query_as::<_, SongRow>(
            r#"
            SELECT id, title, description, link
            FROM songs
            WHERE id = $1
            "#,
        )
        .bind(id as i64)
        .fetch_optional(&self.pool)
        .await
        .map_err(|err| StoreError::Database(err.to_string()))?
        .ok_or(StoreError::NotFound)?;

        Ok(song_from_row(row))
    }

    async fn list(&self, limit: i64) -> Result<Vec<SongRecord>, StoreError> {
        let rows = sqlx::query_as::<_, SongRow>(
            r#"
            SELECT id, title, description, link
            FROM songs
            ORDER BY id
            LIMIT $1
            "#,
        )
        .bind(limit)
        .fetch_all(&self.pool)
        .await
        .map_err(|err| StoreError::Database(err.to_string()))?;

        Ok(rows.into_iter().map(song_from_row).collect())
    }

    async fn update(&self, song: SongRecord) -> Result<SongRecord, StoreError> {
        let row = sqlx::query_as::<_, SongRow>(
            r#"
            UPDATE songs
            SET title = $1, description = $2, link = $3
            WHERE id = $4
            RETURNING id, title, description, link
            "#,
        )
        .bind(song.title)
        .bind(song.description)
        .bind(song.link)
        .bind(song.id as i64)
        .fetch_optional(&self.pool)
        .await
        .map_err(|err| StoreError::Database(err.to_string()))?
        .ok_or(StoreError::NotFound)?;

        Ok(song_from_row(row))
    }

    async fn delete(&self, id: u64) -> Result<(), StoreError> {
        let result = sqlx::query("DELETE FROM songs WHERE id = $1")
            .bind(id as i64)
            .execute(&self.pool)
            .await
            .map_err(|err| StoreError::Database(err.to_string()))?;

        if result.rows_affected() == 0 {
            return Err(StoreError::NotFound);
        }

        Ok(())
    }
}

impl SongServer {
    pub fn new(pool: PgPool) -> Self {
        Self {
            store: Arc::new(PostgresSongStore::new(pool)),
        }
    }

    #[allow(dead_code)]
    pub fn with_store(store: Arc<dyn SongStore>) -> Self {
        Self { store }
    }
}

#[tonic::async_trait]
impl SongService for SongServer {
    async fn create_song(
        &self,
        request: Request<CreateSongRequest>,
    ) -> Result<Response<Song>, Status> {
        let song = request
            .into_inner()
            .song
            .ok_or_else(|| Status::invalid_argument("create_song requires song payload"))?;

        if song.title.trim().is_empty() {
            return Err(Status::invalid_argument("song title is required"));
        }

        let record = SongRecord {
            id: 0,
            title: song.title,
            description: empty_to_none(song.description),
            link: empty_to_none(song.link),
        };
        let record = self.store.create(record).await.map_err(map_store_error)?;
        Ok(Response::new(record_to_song(record)))
    }

    async fn get_song(&self, request: Request<GetSongRequest>) -> Result<Response<Song>, Status> {
        let id = parse_id(&request.into_inner().name)?;

        let record = self.store.get(id as u64).await.map_err(map_store_error)?;
        Ok(Response::new(record_to_song(record)))
    }

    async fn list_songs(
        &self,
        request: Request<ListSongsRequest>,
    ) -> Result<Response<ListSongsResponse>, Status> {
        let limit = sanitize_page_size(request.into_inner().page_size);

        let rows = self.store.list(limit).await.map_err(map_store_error)?;
        let songs = rows.into_iter().map(record_to_song).collect();
        Ok(Response::new(ListSongsResponse {
            songs,
            next_page_token: String::new(),
        }))
    }

    async fn update_song(
        &self,
        request: Request<UpdateSongRequest>,
    ) -> Result<Response<Song>, Status> {
        let request = request.into_inner();
        let song = request
            .song
            .ok_or_else(|| Status::invalid_argument("update_song requires song payload"))?;
        if song.id == 0 {
            return Err(Status::invalid_argument("song id is required"));
        }

        let existing = self.store.get(song.id).await.map_err(map_store_error)?;
        let updated = apply_song_update_mask(&existing, &song, request.update_mask)?;
        if updated.title.trim().is_empty() {
            return Err(Status::invalid_argument("song title is required"));
        }

        let record = SongRecord {
            id: song.id,
            title: updated.title,
            description: empty_to_none(updated.description),
            link: empty_to_none(updated.link),
        };
        let record = self.store.update(record).await.map_err(map_store_error)?;
        Ok(Response::new(record_to_song(record)))
    }

    async fn delete_song(
        &self,
        request: Request<DeleteSongRequest>,
    ) -> Result<Response<()>, Status> {
        let id = parse_id(&request.into_inner().name)?;

        self.store
            .delete(id as u64)
            .await
            .map_err(map_store_error)?;
        Ok(Response::new(()))
    }
}

fn sanitize_page_size(page_size: i32) -> i64 {
    let size = if page_size <= 0 { 100 } else { page_size };
    i64::from(size.min(500))
}

fn parse_id(name: &str) -> Result<i64, Status> {
    name.trim()
        .parse::<i64>()
        .map_err(|_| Status::invalid_argument("invalid id"))
        .and_then(|id| {
            if id <= 0 {
                Err(Status::invalid_argument("invalid id"))
            } else {
                Ok(id)
            }
        })
}

fn empty_to_none(value: String) -> Option<String> {
    let trimmed = value.trim();
    if trimmed.is_empty() {
        None
    } else {
        Some(value)
    }
}

fn song_from_row(row: SongRow) -> SongRecord {
    SongRecord {
        id: row.id as u64,
        title: row.title,
        description: row.description,
        link: row.link,
    }
}

fn record_to_song(record: SongRecord) -> Song {
    Song {
        id: record.id,
        title: record.title,
        description: record.description.unwrap_or_default(),
        link: record.link.unwrap_or_default(),
    }
}

fn apply_song_update_mask(
    existing: &SongRecord,
    incoming: &Song,
    mask: Option<prost_types::FieldMask>,
) -> Result<Song, Status> {
    let mut updated = record_to_song(existing.clone());

    let paths = mask.map(|mask| mask.paths).unwrap_or_else(Vec::new);

    if paths.is_empty() {
        updated.title = incoming.title.clone();
        updated.description = incoming.description.clone();
        updated.link = incoming.link.clone();
        return Ok(updated);
    }

    for path in paths {
        match path.as_str() {
            "title" => updated.title = incoming.title.clone(),
            "description" => updated.description = incoming.description.clone(),
            "link" => updated.link = incoming.link.clone(),
            _ => return Err(Status::invalid_argument("unsupported update_mask path")),
        }
    }

    Ok(updated)
}

fn map_store_error(err: StoreError) -> Status {
    match err {
        StoreError::NotFound => Status::not_found("song not found"),
        StoreError::Database(message) => Status::internal(format!("database error: {message}")),
    }
}

#[cfg(test)]
mod tests {
    use super::{
        SongRecord, SongRow, SongServer, SongStore, StoreError, apply_song_update_mask, parse_id,
        record_to_song, song_from_row,
    };
    use api::pb::Song;
    use api::pb::song_service_client::SongServiceClient;
    use api::pb::song_service_server::SongServiceServer;
    use api::pb::{
        CreateSongRequest, DeleteSongRequest, GetSongRequest, ListSongsRequest, UpdateSongRequest,
    };
    use async_trait::async_trait;
    use sqlx::{PgPool, postgres::PgPoolOptions};
    use std::collections::HashMap;
    use std::net::SocketAddr;
    use std::sync::Arc;
    use std::sync::atomic::{AtomicU64, Ordering};
    use tokio::sync::Mutex;
    use tokio_stream::wrappers::TcpListenerStream;
    use tonic::transport::Channel;
    use tonic::{Request, transport::Server};

    #[test]
    fn parse_id_rejects_invalid_values() {
        assert!(parse_id("").is_err());
        assert!(parse_id("-1").is_err());
        assert!(parse_id("abc").is_err());
    }

    #[test]
    fn update_mask_updates_selected_fields() {
        let existing = SongRecord {
            id: 1,
            title: "Old".to_string(),
            description: Some("Old desc".to_string()),
            link: Some("old".to_string()),
        };
        let incoming = Song {
            id: 1,
            title: "New".to_string(),
            description: "New desc".to_string(),
            link: "new".to_string(),
        };
        let mask = prost_types::FieldMask {
            paths: vec!["title".to_string(), "link".to_string()],
        };

        let updated = apply_song_update_mask(&existing, &incoming, Some(mask)).expect("updated");
        assert_eq!(updated.title, "New");
        assert_eq!(updated.link, "new");
        assert_eq!(updated.description, "Old desc");
    }

    #[test]
    fn row_to_song_defaults_missing_fields() {
        let row = SongRow {
            id: 2,
            title: "Title".to_string(),
            description: None,
            link: None,
        };
        let song = record_to_song(song_from_row(row));
        assert_eq!(song.description, "");
        assert_eq!(song.link, "");
    }

    #[derive(Debug)]
    struct MockSongStore {
        data: Mutex<HashMap<u64, SongRecord>>,
        next_id: AtomicU64,
        _pool: PgPool,
    }

    impl MockSongStore {
        fn new() -> Self {
            Self {
                data: Mutex::new(HashMap::new()),
                next_id: AtomicU64::new(1),
                _pool: PgPoolOptions::new()
                    .connect_lazy("postgres://postgres:postgres@localhost/postgres")
                    .expect("stub pool"),
            }
        }
    }

    #[async_trait]
    impl SongStore for MockSongStore {
        async fn create(&self, mut song: SongRecord) -> Result<SongRecord, StoreError> {
            let id = self.next_id.fetch_add(1, Ordering::SeqCst);
            song.id = id;
            self.data.lock().await.insert(id, song.clone());
            Ok(song)
        }

        async fn get(&self, id: u64) -> Result<SongRecord, StoreError> {
            self.data
                .lock()
                .await
                .get(&id)
                .cloned()
                .ok_or(StoreError::NotFound)
        }

        async fn list(&self, limit: i64) -> Result<Vec<SongRecord>, StoreError> {
            let mut values: Vec<_> = self.data.lock().await.values().cloned().collect();
            values.sort_by_key(|song| song.id);
            values.truncate(limit as usize);
            Ok(values)
        }

        async fn update(&self, song: SongRecord) -> Result<SongRecord, StoreError> {
            let mut data = self.data.lock().await;
            if !data.contains_key(&song.id) {
                return Err(StoreError::NotFound);
            }
            data.insert(song.id, song.clone());
            Ok(song)
        }

        async fn delete(&self, id: u64) -> Result<(), StoreError> {
            let mut data = self.data.lock().await;
            if data.remove(&id).is_none() {
                return Err(StoreError::NotFound);
            }
            Ok(())
        }
    }

    async fn start_server(
        store: Arc<dyn SongStore>,
    ) -> Option<(SocketAddr, tokio::task::JoinHandle<()>)> {
        let addr: SocketAddr = "127.0.0.1:0".parse().expect("addr");
        let listener = match tokio::net::TcpListener::bind(&addr).await {
            Ok(listener) => listener,
            Err(err) if err.kind() == std::io::ErrorKind::PermissionDenied => return None,
            Err(err) => panic!("bind failed: {err}"),
        };
        let addr = listener.local_addr().expect("local addr");
        let service = SongServiceServer::new(SongServer::with_store(store));

        let handle = tokio::spawn(async move {
            Server::builder()
                .add_service(service)
                .serve_with_incoming(TcpListenerStream::new(listener))
                .await
                .expect("grpc server failed");
        });

        tokio::time::sleep(tokio::time::Duration::from_millis(50)).await;
        Some((addr, handle))
    }

    async fn create_client(addr: SocketAddr) -> SongServiceClient<Channel> {
        let endpoint = format!("http://{}:{}", addr.ip(), addr.port());
        SongServiceClient::connect(endpoint).await.expect("connect")
    }

    #[tokio::test]
    async fn e2e_song_crud() {
        let store = Arc::new(MockSongStore::new());
        let Some((addr, _handle)) = start_server(store).await else {
            eprintln!("skipping e2e_song_crud: tcp bind not permitted");
            return;
        };
        let mut client = create_client(addr).await;

        let create = CreateSongRequest {
            parent: String::new(),
            song_id: String::new(),
            song: Some(Song {
                id: 0,
                title: "Song".to_string(),
                description: "Desc".to_string(),
                link: "Link".to_string(),
            }),
        };
        let created = client
            .create_song(Request::new(create))
            .await
            .expect("create")
            .into_inner();

        let fetched = client
            .get_song(Request::new(GetSongRequest {
                name: created.id.to_string(),
            }))
            .await
            .expect("get")
            .into_inner();
        assert_eq!(fetched.title, "Song");

        let list = client
            .list_songs(Request::new(ListSongsRequest {
                parent: String::new(),
                page_size: 10,
                page_token: String::new(),
            }))
            .await
            .expect("list")
            .into_inner();
        assert_eq!(list.songs.len(), 1);

        let update = UpdateSongRequest {
            song: Some(Song {
                id: created.id,
                title: "Song 2".to_string(),
                description: "Desc 2".to_string(),
                link: "Link 2".to_string(),
            }),
            update_mask: None,
        };
        let updated = client
            .update_song(Request::new(update))
            .await
            .expect("update")
            .into_inner();
        assert_eq!(updated.title, "Song 2");

        client
            .delete_song(Request::new(DeleteSongRequest {
                name: created.id.to_string(),
            }))
            .await
            .expect("delete");
    }
}
