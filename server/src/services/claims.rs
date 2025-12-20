use serde::{Deserialize, Serialize};

#[derive(Debug, Serialize, Deserialize)]
pub struct Claims {
    pub sub: u64,
    pub exp: usize,
    pub iat: usize,
    pub is_admin: bool,
}
