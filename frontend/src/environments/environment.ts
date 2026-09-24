// Production environment
// API_URL is injected from environment variables during build
export const environment = {
    API_URL: (window as any).env?.API_URL || ''
};
