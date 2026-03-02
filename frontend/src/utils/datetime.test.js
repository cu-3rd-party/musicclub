import { describe, expect, it } from "vitest";
import {
  formatDate,
  timestampToDate,
  toInputValue,
  toTimestamp,
} from "./datetime";

describe("datetime utils", () => {
  it("formatDate returns fallback for empty value", () => {
    expect(formatDate()).toBe("Дата не задана");
  });

  it("formatDate formats day and time", () => {
    const date = new Date(2024, 0, 2, 3, 4);
    const formatted = formatDate(date);
    expect(formatted).toMatch(/02/);
    expect(formatted).toMatch(/03:04/);
  });

  it("timestampToDate converts proto timestamp", () => {
    const ts = { seconds: 1700000000, nanos: 500000000 };
    const date = timestampToDate(ts);
    expect(date.getTime()).toBe(1700000000 * 1000 + 500);
  });

  it("timestampToDate handles bigint seconds", () => {
    const ts = { seconds: BigInt(1700000001), nanos: 0 };
    const date = timestampToDate(ts);
    expect(date.getTime()).toBe(1700000001 * 1000);
  });

  it("toInputValue formats for datetime-local", () => {
    const date = new Date(2024, 10, 9, 7, 6);
    expect(toInputValue(date)).toBe("2024-11-09T07:06");
  });

  it("toTimestamp converts date to proto timestamp", () => {
    const date = new Date(2024, 5, 1, 12, 30, 5, 250);
    const ts = toTimestamp(date);
    expect(ts.seconds).toBe(BigInt(Math.floor(date.getTime() / 1000)));
    expect(ts.nanos).toBe(date.getMilliseconds() * 1_000_000);
  });
});
