import AuthGate from "./AuthGate";
import "../styles/global.css";

const App = () => {
	return (
		<div className="app-shell">
			<AuthGate />
		</div>
	);
};

export default App;
