<script lang="ts">
    import * as InputGroup from "$lib/components/ui/input-group"
    import * as DropdownMenu from "$lib/components/ui/dropdown-menu";
    import {Ellipsis, SearchIcon} from "@lucide/svelte";
    import {Checkbox} from "$lib/components/ui/checkbox";
</script>

<main class="w-full h-full flex flex-col">
    <div class="p-4">
        <InputGroup.Root>
            <InputGroup.Addon>
                <SearchIcon/>
            </InputGroup.Addon>
            <InputGroup.Input placeholder="Название песни"/>
            <InputGroup.Addon align="inline-end">
                <DropdownMenu.Root>
                    <DropdownMenu.Trigger>
                        {#snippet child({props})}
                            <InputGroup.Button
                                {...props}
                                variant="ghost"
                                aria-label="More"
                                size="icon-xs"
                            >
                                <Ellipsis/>
                            </InputGroup.Button>
                        {/snippet}
                    </DropdownMenu.Trigger>
                    <DropdownMenu.Content align="end">
                        <DropdownMenu.Item>Роли</DropdownMenu.Item>
                        <DropdownMenu.Item>
                            <Checkbox/>
                            Избранные
                        </DropdownMenu.Item>
                    </DropdownMenu.Content>
                </DropdownMenu.Root>
            </InputGroup.Addon>
        </InputGroup.Root>
    </div>
    <div class="w-full h-full">
<!-- сюда разместить вечно листающийся грид из карточек песен -->
    </div>
</main>
