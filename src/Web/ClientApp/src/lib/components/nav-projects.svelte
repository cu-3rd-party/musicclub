<script lang="ts">
    import {goto} from "$app/navigation";
    import {activateQuerySession} from "$lib/ask/query-sessions";
    import type {SidebarQuerySession} from "$lib/ask/query-sessions";
    import * as Sidebar from "$lib/components/ui/sidebar/index.js";
    import {resolve} from "$app/paths";

    let {
        queries,
    }: {
        queries: SidebarQuerySession[];
    } = $props();

    async function handleSelectSession(sessionId: string) {
        activateQuerySession(sessionId);
        await goto(resolve(`/data/ask/session/${sessionId}`));
    }
</script>

<Sidebar.Group class="group-data-[collapsible=icon]:hidden flex min-h-0 flex-1 flex-col">
    <Sidebar.GroupLabel>Предыдущие запросы</Sidebar.GroupLabel>
    <Sidebar.GroupContent class="min-h-0 flex-1 overflow-hidden">
        <div class="no-scrollbar flex h-full flex-col gap-2 overflow-y-auto pr-1">
            {#each queries as item (item.id)}
                <button
                        type="button"
                        class={item.active
						? "bg-sidebar-accent text-sidebar-accent-foreground ring-sidebar-ring/40 flex w-full flex-col rounded-xl border border-sidebar-border/60 p-3 text-left shadow-sm ring-1"
						: "hover:bg-sidebar-accent/70 hover:text-sidebar-accent-foreground flex w-full flex-col rounded-xl border border-transparent p-3 text-left transition-colors"}
                        onclick={() => void handleSelectSession(item.id)}
                >
                    <div class="mb-1 flex items-center justify-between gap-3">
                        <p class="line-clamp-1 text-sm font-medium">{item.name}</p>
                        <span class="text-muted-foreground shrink-0 text-[11px]">{item.time}</span>
                    </div>
                    <p class="text-muted-foreground line-clamp-2 text-xs leading-5">{item.preview}</p>
                </button>
            {/each}
        </div>
    </Sidebar.GroupContent>
</Sidebar.Group>