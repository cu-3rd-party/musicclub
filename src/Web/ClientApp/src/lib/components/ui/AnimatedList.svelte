<script lang="ts">
    import {onMount, untrack} from 'svelte';
    import CheckIcon from '@lucide/svelte/icons/check';
    import Trash2Icon from '@lucide/svelte/icons/trash-2';

    type Props = {
        items?: string[];
        onItemSelect?: (item: string, index: number) => void;
        showGradients?: boolean;
        enableArrowNavigation?: boolean;
        class?: string;
        itemClass?: string;
        listClass?: string;
        itemBadges?: string[][];
        itemProgresses?: Array<number | undefined>;
        itemProgressLabels?: Array<string | undefined>;
        itemProgressClasses?: Array<string | undefined>;
        itemMessages?: string[];
        itemSucceeded?: boolean[];
        displayScrollbar?: boolean;
        onRemove?: (index: number) => void;
        removeDisabled?: boolean;
        initialSelectedIndex?: number;
    };

    let {
        items = [
            'Item 1', 'Item 2', 'Item 3', 'Item 4', 'Item 5',
            'Item 6', 'Item 7', 'Item 8', 'Item 9', 'Item 10',
            'Item 11', 'Item 12', 'Item 13', 'Item 14', 'Item 15'
        ],
        onItemSelect,
        showGradients = true,
        enableArrowNavigation = true,
        class: className = '',
        itemClass = '',
        displayScrollbar = true,
        listClass = 'max-h-[400px]',
        itemBadges = [] as string[][],
        itemProgresses = [],
        itemProgressLabels = [],
        itemProgressClasses = [],
        itemMessages = [],
        itemSucceeded = [],
        onRemove,
        removeDisabled = false,
        initialSelectedIndex = -1
    }: Props = $props();

    let listRef: HTMLDivElement;
    let selectedIndex = $state(-1);
    let keyboardNav = $state(false);
    let topGradientOpacity = $state(0);
    let bottomGradientOpacity = $state(1);
    let inView = $state<boolean[]>([]);

    $effect(() => {
        if (inView.length !== items.length) {
            untrack(() => {
                inView = items.map((_, i) => inView[i] ?? false);
            });
        }
    });

    function getDisplayItem(item: string): string {
        const base = item.split('/').pop() || item;
        const dot = base.lastIndexOf('.');

        return dot > 0 ? base.slice(0, dot) : base;
    }

    function handleScroll(e: Event) {
        const t = e.currentTarget as HTMLDivElement;
        topGradientOpacity = Math.min(t.scrollTop / 50, 1);
        const bottomDistance = t.scrollHeight - (t.scrollTop + t.clientHeight);
        bottomGradientOpacity = t.scrollHeight <= t.clientHeight ? 0 : Math.min(bottomDistance / 50, 1);
    }

    function inViewAction(node: HTMLElement, index: number) {
        const io = new IntersectionObserver(
            (entries) => {
                for (const entry of entries) {
                    inView[index] = entry.intersectionRatio >= 0.5;
                }
            },
            {root: listRef, threshold: [0, 0.5, 1]}
        );
        io.observe(node);
        return {destroy: () => io.disconnect()};
    }

    onMount(() => {
        selectedIndex = initialSelectedIndex;
        if (!enableArrowNavigation) return;
        const handler = (e: KeyboardEvent) => {
            if (e.key === 'ArrowDown' || (e.key === 'Tab' && !e.shiftKey)) {
                e.preventDefault();
                keyboardNav = true;
                selectedIndex = Math.min(selectedIndex + 1, items.length - 1);
            } else if (e.key === 'ArrowUp' || (e.key === 'Tab' && e.shiftKey)) {
                e.preventDefault();
                keyboardNav = true;
                selectedIndex = Math.max(selectedIndex - 1, 0);
            } else if (e.key === 'Enter') {
                if (selectedIndex >= 0 && selectedIndex < items.length) {
                    e.preventDefault();
                    onItemSelect?.(items[selectedIndex], selectedIndex);
                }
            }
        };
        window.addEventListener('keydown', handler);
        return () => window.removeEventListener('keydown', handler);
    });

    $effect(() => {
        if (!keyboardNav || selectedIndex < 0 || !listRef) return;
        const container = listRef;
        const selectedItem = container.querySelector(
            `[data-index="${selectedIndex}"]`
        ) as HTMLElement | null;
        if (selectedItem) {
            const extraMargin = 50;
            const itemTop = selectedItem.offsetTop;
            const itemBottom = itemTop + selectedItem.offsetHeight;
            if (itemTop < container.scrollTop + extraMargin) {
                container.scrollTo({top: itemTop - extraMargin, behavior: 'smooth'});
            } else if (itemBottom > container.scrollTop + container.clientHeight - extraMargin) {
                container.scrollTo({
                    top: itemBottom - container.clientHeight + extraMargin,
                    behavior: 'smooth'
                });
            }
        }
        untrack(() => (keyboardNav = false));
    });
</script>

<div class="relative w-125 {className}">
    <div
        bind:this={listRef}
        class="al-scroll overflow-y-auto {listClass} {displayScrollbar ? 'al-scrollbar' : 'al-scrollbar-hide'}"
        onscroll={handleScroll}
    >
        {#each items as item, index (index)}
            <div
                use:inViewAction={index}
                data-index={index}
                class="mb-4 cursor-pointer al-item"
                style:transform={inView[index] ? 'scale(1)' : 'scale(0.7)'}
                style:opacity={inView[index] ? 1 : 0}
                style:transition="transform 0.2s ease 0.1s, opacity 0.2s ease 0.1s"
                onmouseenter={() => (selectedIndex = index)}
                onclick={() => {
					selectedIndex = index;
					onItemSelect?.(item, index);
				}}
                onkeydown={(e) => {
					if (e.key === 'Enter' || e.key === ' ') {
						e.preventDefault();
						selectedIndex = index;
						onItemSelect?.(item, index);
					}
				}}
                role="option"
                aria-selected={selectedIndex === index}
                tabindex="-1"
            >
                <div
                    class="flex items-center gap-3 p-4 bg-[#222] rounded-lg {selectedIndex === index ? 'al-selected' : ''} {itemClass}"
                >
                    <p class="text-white m-0 min-w-0 flex-1 truncate">{getDisplayItem(item)}</p>
                    {#if itemProgresses[index] !== undefined}
                        <div class="ml-auto w-24 shrink-0">
                            {#if itemProgressLabels[index]}
                                <p class="m-0 text-right text-[11px] text-white/60">{itemProgressLabels[index]}</p>
                            {/if}
                            <div class="mt-1 h-1.5 overflow-hidden rounded-full bg-white/10">
                                <div
                                    class="h-full rounded-full transition-[width] {itemProgressClasses[index] || 'bg-primary'}"
                                    style:width={`${itemProgresses[index]}%`}
                                ></div>
                            </div>
                        </div>
                    {/if}
                    {#if itemBadges && itemBadges[index]}
                        {#each itemBadges[index] as badge (badge)}
                            <span
                                class="shrink-0 rounded-md bg-white/10 px-2 py-0.5 text-xs text-white/70">{badge}</span>
                        {/each}
                    {/if}
                    {#if itemSucceeded[index]}
                        <span
                            class="flex size-7 shrink-0 items-center justify-center rounded-md bg-emerald-400/15 text-emerald-400"
                            aria-label="Файл загружен"
                        >
                            <CheckIcon class="size-4"/>
                        </span>
                    {:else if onRemove}
                        <button
                            type="button"
                            class="flex size-7 shrink-0 items-center justify-center rounded-md text-white/40 transition-colors hover:bg-white/10 hover:text-white/80 disabled:pointer-events-none disabled:opacity-40"
                            onclick={(e) => { e.stopPropagation(); onRemove(index); }}
                            disabled={removeDisabled}
                        >
                            <Trash2Icon class="size-3.5"/>
                        </button>
                    {/if}
                </div>
                {#if itemMessages[index]}
                    <p class="text-destructive mt-2 px-4 text-sm">{itemMessages[index]}</p>
                {/if}
            </div>
        {/each}
    </div>
    {#if showGradients}
        <div
            class="absolute top-0 left-0 right-0 h-12.5 pointer-events-none"
            style="background: linear-gradient(to bottom, #14110E, transparent); opacity: {topGradientOpacity}; transition: opacity 0.3s ease;"
        ></div>
        <div
            class="absolute bottom-0 left-0 right-0 h-25 pointer-events-none"
            style="background: linear-gradient(to top, #14110E, transparent); opacity: {bottomGradientOpacity}; transition: opacity 0.3s ease;"
        ></div>
    {/if}
</div>
