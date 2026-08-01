const defaultApiUrl = "";

// по сути используется только если оно насильно задано, по умолчанию запросы идут к same origin
export const API_URL = import.meta.env.API_URL?.trim() || defaultApiUrl;
