import { Component, type ErrorInfo, type ReactNode } from "react";
import "../styles/components/error-boundary.css";

type Props = {
	children: ReactNode;
};

type State = {
	error: Error | null;
};

class ErrorBoundary extends Component<Props, State> {
	state: State = { error: null };

	static getDerivedStateFromError(error: Error) {
		return { error };
	}

	componentDidCatch(error: Error, errorInfo: ErrorInfo) {
		console.error("App crash:", error, errorInfo);
	}

	render() {
		if (this.state.error) {
			return (
				<div className="card error-boundary__card">
					<div className="card-title">Ошибка приложения</div>
					<div className="error-boundary__message">
						{this.state.error.message || "Неизвестная ошибка"}
					</div>
					<pre className="error-boundary__stack">
						{this.state.error.stack}
					</pre>
				</div>
			);
		}

		return this.props.children;
	}
}

export default ErrorBoundary;
