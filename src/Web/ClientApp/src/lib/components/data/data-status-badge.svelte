<script lang="ts">
    import {Badge} from "$lib/components/ui/badge";
    import type {KnowledgeObjectStatus} from "$lib/data/types";

    const statusMap: Record<KnowledgeObjectStatus, {
        label: string;
        variant: "secondary" | "default" | "destructive" | "outline"
    }> = {
        pending: {label: "В очереди", variant: "outline"},
        processing: {label: "Обработка", variant: "secondary"},
        ready: {label: "Готов", variant: "default"},
        failed: {label: "Ошибка", variant: "destructive"},
    };

    let {status}: { status: KnowledgeObjectStatus } = $props();
</script>

<Badge variant={statusMap[status].variant}>{statusMap[status].label}</Badge>
