<script lang="ts">
    import {marked, type Tokens} from "marked";

    let {markdown}: { markdown: string } = $props();

    type MarkdownToken = Tokens.Generic;

    const tokens = $derived(marked.lexer(markdown ?? ""));

    function inlineTokens(text: string): MarkdownToken[] {
        return marked.Lexer.lexInline(text ?? "");
    }

    function hrefLabel(token: MarkdownToken): string {
        return "text" in token && typeof token.text === "string" ? token.text : token.raw;
    }
</script>

{#snippet renderInline(items: MarkdownToken[])}
    {#each items as item, index (`${item.type}-${index}`)}
        {#if item.type === "text"}
            {item.raw}
        {:else if item.type === "strong"}
            <strong>{@render renderInline(item.tokens ?? inlineTokens(item.raw))}</strong>
        {:else if item.type === "em"}
            <em>{@render renderInline(item.tokens ?? inlineTokens(item.raw))}</em>
        {:else if item.type === "codespan"}
            <code>{item.text}</code>
        {:else if item.type === "br"}
            <br/>
        {:else if item.type === "link"}
			<span class="text-primary underline underline-offset-4">
				{@render renderInline(item.tokens ?? inlineTokens(hrefLabel(item)))}
			</span>
        {:else if item.type === "del"}
            <s>{@render renderInline(item.tokens ?? inlineTokens(item.raw))}</s>
        {:else}
            {item.raw}
        {/if}
    {/each}
{/snippet}

{#snippet renderBlocks(items: MarkdownToken[])}
    {#each items as item, index (`${item.type}-${index}`)}
        {#if item.type === "heading"}
            {#if item.depth === 1}
                <h1 class="text-3xl font-semibold tracking-tight text-foreground md:text-4xl">{@render renderInline(item.tokens ?? [])}</h1>
            {:else if item.depth === 2}
                <h2 class="text-2xl font-semibold tracking-tight text-foreground md:text-3xl">{@render renderInline(item.tokens ?? [])}</h2>
            {:else if item.depth === 3}
                <h3 class="text-xl font-semibold tracking-tight text-foreground md:text-2xl">{@render renderInline(item.tokens ?? [])}</h3>
            {:else}
                <h4 class="text-lg font-semibold tracking-tight text-foreground">{@render renderInline(item.tokens ?? [])}</h4>
            {/if}
        {:else if item.type === "paragraph"}
            <p class="leading-7 text-foreground/90">{@render renderInline(item.tokens ?? [])}</p>
        {:else if item.type === "list"}
            {#if item.ordered}
                <ol class="list-decimal space-y-2 pl-6 text-foreground/90 marker:text-muted-foreground">
                    {#each item.items as listItem, listIndex (`${listItem.text}-${listIndex}`)}
                        <li>{@render renderInline(listItem.tokens ?? inlineTokens(listItem.text))}</li>
                    {/each}
                </ol>
            {:else}
                <ul class="list-disc space-y-2 pl-6 text-foreground/90 marker:text-muted-foreground">
                    {#each item.items as listItem, listIndex (`${listItem.text}-${listIndex}`)}
                        <li>{@render renderInline(listItem.tokens ?? inlineTokens(listItem.text))}</li>
                    {/each}
                </ul>
            {/if}
        {:else if item.type === "blockquote"}
            <blockquote class="border-border/70 text-muted-foreground border-l-2 pl-4 italic">
                {@render renderBlocks(item.tokens ?? [])}
            </blockquote>
        {:else if item.type === "code"}
            <pre class="bg-background overflow-x-auto rounded-xl border border-border/60 p-4"><code>{item.text}</code></pre>
        {:else if item.type === "hr"}
            <hr class="border-border/60"/>
        {:else if item.type !== "space"}
            <p class="leading-7 text-foreground/90">{item.raw}</p>
        {/if}
    {/each}
{/snippet}

<div class="max-w-none space-y-5 break-words text-base">
    {@render renderBlocks(tokens)}
</div>
