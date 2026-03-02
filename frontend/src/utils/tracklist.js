export function parseTracklist(text) {
  return text
    .split("\n")
    .map((line) => line.trim())
    .filter(Boolean)
    .map((line, idx) => {
      const [titlePart, artistPart] = line.split("—").map((p) => p.trim());
      return {
        order: idx + 1,
        songId: "",
        customTitle: titlePart || `Трек ${idx + 1}`,
        customArtist: artistPart || "",
      };
    });
}
