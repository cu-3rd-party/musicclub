import { create } from "@bufbuild/protobuf";
import { type Timestamp, TimestampSchema } from "@bufbuild/protobuf/wkt";

export function makeTimestamp(value: string): Timestamp | undefined {
	if (!value) {
		return undefined;
	}
	const date = new Date(value);
	const millis = date.getTime();
	if (Number.isNaN(millis)) {
		return undefined;
	}
	return create(TimestampSchema, {
		seconds: BigInt(Math.floor(millis / 1000)),
		nanos: (millis % 1000) * 1000000,
	});
}
