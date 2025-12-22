all: help

help:
	@echo "Makefile commands:"
	@echo "  all          - Show this help message"
	@echo "  setup-proto  - Install protoc, Go plugins, and npm deps needed for codegen"
	@echo "  generate     - Generate code from .proto files"

setup-proto:
	@echo "Installing protoc and Go plugins..."
	@which protoc >/dev/null 2>&1 || (sudo apt-get update && sudo apt-get install -y protobuf-compiler)
	@go install google.golang.org/protobuf/cmd/protoc-gen-go@latest
	@go install google.golang.org/grpc/cmd/protoc-gen-go-grpc@latest
	@echo "Ensuring frontend proto npm deps are installed..."
	@cd frontend && npm install
	@echo "Proto toolchain is ready."

generate:
	@cd frontend && npx buf generate --clean
	@echo "Generated TypeScript code from .proto files."
	@protoc --proto_path=proto \
		--go_out=backend/proto --go_opt=paths=source_relative \
		--go-grpc_out=backend/proto --go-grpc_opt=paths=source_relative \
		proto/*.proto
	@echo "Generated Go code from proto files."

.PHONY: all help setup-proto generate
