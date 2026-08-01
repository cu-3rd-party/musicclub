<script lang="ts">
    import {Button} from "$lib/components/ui/button/index.js";
    import UploadIcon from "@lucide/svelte/icons/upload";

    let {
        files = $bindable<File[]>([]),
        disabled = false,
    }: {
        files?: File[];
        disabled?: boolean;
    } = $props();

    let inputElement = $state<HTMLInputElement | null>(null);
    let isDragging = $state(false);

    function mergeFiles(nextFiles: File[]): void {
        const mergedFiles = [...files];

        for (const file of nextFiles) {
            const fileKey = getFileKey(file);
            if (!mergedFiles.some((currentFile) => getFileKey(currentFile) === fileKey)) {
                mergedFiles.push(file);
            }
        }

        files = mergedFiles;
    }

    function handleInputChange(event: Event): void {
        const target = event.currentTarget as HTMLInputElement;
        mergeFiles(Array.from(target.files ?? []));
        target.value = "";
    }

    function handleDrop(event: DragEvent): void {
        event.preventDefault();
        isDragging = false;

        if (disabled) {
            return;
        }

        mergeFiles(Array.from(event.dataTransfer?.files ?? []));
    }

    function getFileKey(file: File): string {
        return `${file.name}:${file.size}:${file.lastModified}`;
    }
</script>

<div
        role="group"
        aria-label="Зона загрузки документов"
        class={`rounded-[1.75rem] border border-dashed px-6 py-10 text-center transition-colors ${isDragging ? "border-foreground bg-muted/80" : "border-border bg-background/70"} ${disabled ? "opacity-60" : ""}`}
        ondragenter={(event) => {
		event.preventDefault();
		if (!disabled) {
			isDragging = true;
		}
	}}
        ondragover={(event) => event.preventDefault()}
        ondragleave={(event) => {
		event.preventDefault();
		isDragging = false;
	}}
        ondrop={handleDrop}
>
    <input
            bind:this={inputElement}
            type="file"
            multiple
            class="sr-only"
            disabled={disabled}
            onchange={handleInputChange}
    />

    <div class="mx-auto flex max-w-xl flex-col items-center gap-4">
        <div class="bg-muted text-muted-foreground flex size-14 items-center justify-center rounded-2xl">
            <UploadIcon class="size-6"/>
        </div>
        <div class="space-y-2">
            <h2 class="text-foreground text-xl font-semibold">Перетащите документы сюда</h2>
            <p class="text-muted-foreground text-sm leading-6">
                Поддерживается множественная загрузка. Можно выбрать PDF, DOCX, архивы и другие файлы, которые затем
                будут проиндексированы.
            </p>
        </div>
        <Button type="button" variant="outline" class="rounded-full" disabled={disabled}
                onclick={() => inputElement?.click()}>
            Выбрать файлы
        </Button>
        <p class="text-muted-foreground text-xs">Перетащите файлы мышью или нажмите кнопку выбора.</p>
    </div>
</div>
