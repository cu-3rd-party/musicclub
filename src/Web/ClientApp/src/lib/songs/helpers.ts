function getYouTubeVideoId(url: string): string | null {
    try {
        const parsed = new URL(url);

        // youtube.com/watch?v=...
        if (
            parsed.hostname === "youtube.com" ||
            parsed.hostname === "www.youtube.com" ||
            parsed.hostname === "m.youtube.com"
        ) {
            return parsed.searchParams.get("v");
        }

        // youtu.be/...
        if (parsed.hostname === "youtu.be") {
            return parsed.pathname.slice(1).split("/")[0] || null;
        }

        return null;
    } catch {
        return null;
    }
}

function getYouTubeThumbnail(url: string): string | null {
    const videoId = getYouTubeVideoId(url);

    if (!videoId) {
        return null;
    }

    return `https://img.youtube.com/vi/${videoId}/hqdefault.jpg`;
}

export async function fetchYouTubeThumbnail(url: string): Promise<File | null> {
    const thumbnailUrl = getYouTubeThumbnail(url);

    if (!thumbnailUrl) {
        return null;
    }

    const response = await fetch(thumbnailUrl);

    if (!response.ok) {
        return null;
    }

    const blob = await response.blob();

    return new File([blob], "youtube-thumbnail.jpg", {
        type: blob.type || "image/jpeg",
    });
}
