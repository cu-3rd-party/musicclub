import type { Timestamp } from "@bufbuild/protobuf/wkt";

export function toDateInput(ts?: Timestamp): string {
	if (!ts) {
		return "";
	}
	const millis = Number(ts.seconds) * 1000 + Math.floor((ts.nanos ?? 0) / 1000000);
	return new Date(millis).toISOString().slice(0, 16);
}
