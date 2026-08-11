<script lang="ts">
    const labels: Record<string, string> = {
        ru: "RU",
        en: "EN",
        auto: "AUTO",
    };

    let {language}: { language?: string | null } = $props();
    let normalized = $derived(language?.toLowerCase() || "auto");
</script>

<span
    class="inline-flex h-6 min-w-10 items-center justify-center rounded border border-border/40 bg-muted/30 px-2 text-xs font-medium text-muted-foreground">
	{labels[normalized] ?? normalized.toUpperCase()}
</span>
