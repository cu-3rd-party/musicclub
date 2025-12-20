import type { Timestamp } from "@bufbuild/protobuf/wkt";

export function formatTimestamp(ts?: Timestamp): string {
	if (!ts) {
		return "TBD";
	}
	const millis = Number(ts.seconds) * 1000 + Math.floor((ts.nanos ?? 0) / 1000000);
	return new Date(millis).toLocaleString();
}
