SELECT 'CREATE DATABASE bot'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'bot')\gexec
