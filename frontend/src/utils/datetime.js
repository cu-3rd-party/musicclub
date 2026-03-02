import { create } from "@bufbuild/protobuf";
import { TimestampSchema } from "@bufbuild/protobuf/wkt";

export function formatDate(date) {
  if (!date) return "Дата не задана";
  return date.toLocaleString("ru-RU", {
    day: "2-digit",
    month: "short",
    hour: "2-digit",
    minute: "2-digit",
  });
}

export function timestampToDate(ts) {
  if (!ts) return undefined;
  const seconds =
    typeof ts.seconds === "bigint" ? Number(ts.seconds) : (ts.seconds ?? 0);
  const nanos = ts.nanos ?? 0;
  return new Date(seconds * 1000 + Math.floor(nanos / 1_000_000));
}

export function toInputValue(date) {
  if (!date) return "";
  const pad = (n) => n.toString().padStart(2, "0");
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

export function toTimestamp(date) {
  return create(TimestampSchema, {
    seconds: BigInt(Math.floor(date.getTime() / 1000)),
    nanos: date.getMilliseconds() * 1_000_000,
  });
}
