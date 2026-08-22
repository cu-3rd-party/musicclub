import { readdir } from "node:fs/promises";
import { join, extname } from "node:path";
import type { LayoutServerLoad } from "./$types";

const BG_DIR = "static/bg";
const IMAGE_EXTENSIONS = new Set([".jpg", ".jpeg", ".png", ".webp", ".avif"]);

async function* walk(dir: string): AsyncGenerator<string> {
    for (const entry of await readdir(dir, { withFileTypes: true })) {
        const path = join(dir, entry.name);
        if (entry.isDirectory()) {
            yield* walk(path);
        } else if (IMAGE_EXTENSIONS.has(extname(entry.name).toLowerCase())) {
            yield path;
        }
    }
}

const toPublicPath = (path: string) =>
    "/" + path.replace(/\\/g, "/").replace(/^static\//, "");
let bgImagesCache: Promise<string[]> | undefined;

function getBgImages(): Promise<string[]> {
    return (bgImagesCache ??= (async () => {
        const images: string[] = [];
        for await (const path of walk(BG_DIR)) {
            images.push(toPublicPath(path));
        }
        return images;
    })());
}

export const load: LayoutServerLoad = async () => {
    const bgImages = await getBgImages();
    return {
        bgImage: bgImages[Math.floor(Math.random() * bgImages.length)],
    };
};
