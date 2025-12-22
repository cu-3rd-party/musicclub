all: help

help:
	@echo "Makefile commands:"
	@echo "  all          - Show this help message"
	@echo "  generate     - Generate code from .proto files"

generate:
	@cd frontend && npx buf generate --clean
	@echo "Generated TypeScript code from .proto files."
	@protoc --proto_path=proto \
		--go_out=backend/proto --go_opt=paths=source_relative \
		--go-grpc_out=backend/proto --go-grpc_opt=paths=source_relative \
		proto/*.proto
	@echo "Generated Go code from proto files."
