all: help

help:
	@echo "Makefile commands:"
	@echo "  all                - Show this help message"
	@echo "  setup-proto        - Install protoc, Go plugins, and npm deps needed for codegen (Ubuntu/Debian)"
	@echo "  setup-proto-fedora  - Install protoc, Go plugins, and npm deps needed for codegen (Fedora/RHEL/CentOS)"
	@echo "  generate           - Generate code from .proto files"

setup-proto:
	@echo "Installing protoc and Go plugins (Ubuntu/Debian)..."
	@which protoc >/dev/null 2>&1 || (sudo apt-get update && sudo apt-get install -y protobuf-compiler)
	@GOBIN="$$(go env GOPATH)/bin" go install google.golang.org/protobuf/cmd/protoc-gen-go@v1.27.1
	@GOBIN="$$(go env GOPATH)/bin" go install google.golang.org/grpc/cmd/protoc-gen-go-grpc@v1.2.0
	@echo "Ensuring GOPATH/bin is on PATH for protoc plugins..."
	@echo "  export PATH=\"$$(go env GOPATH)/bin:$$PATH\""
	@echo "Ensuring frontend proto npm deps are installed..."
	@cd frontend && npm install
	@echo "Proto toolchain is ready."

setup-proto-fedora:
	@echo "Installing protoc and Go plugins (Fedora/RHEL/CentOS)..."
	@which protoc >/dev/null 2>&1 || (sudo dnf install -y protobuf-compiler)
	@GOBIN="$$(go env GOPATH)/bin" go install google.golang.org/protobuf/cmd/protoc-gen-go@v1.27.1
	@GOBIN="$$(go env GOPATH)/bin" go install google.golang.org/grpc/cmd/protoc-gen-go-grpc@v1.2.0
	@echo "Ensuring GOPATH/bin is on PATH for protoc plugins..."
	@echo "  export PATH=\"$$(go env GOPATH)/bin:$$PATH\""
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

.PHONY: all help setup-proto setup-proto-fedora generate
