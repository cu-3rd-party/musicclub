<script lang="ts">
    import * as InputGroup from "$lib/components/ui/input-group"
    import * as DropdownMenu from "$lib/components/ui/dropdown-menu";
    import {ArrowUp, Ellipsis, Plus, SearchIcon, Settings} from "@lucide/svelte";
    import {Checkbox} from "$lib/components/ui/checkbox";
    import SongCard from "$lib/components/song-card.svelte";
    import {Button} from "$lib/components/ui/button";

    let showScrollTop = $state(false);

    function observe(element: HTMLElement) {
        const observer = new IntersectionObserver(
            ([entry]) => {
                showScrollTop = !entry.isIntersecting;
            },
            {
                threshold: 0
            }
        );

        observer.observe(element);

        return {
            destroy() {
                observer.disconnect();
            }
        };
    }

    function scrollToTop() {
        window.scrollTo({
            top: 0,
            behavior: "smooth"
        });
    }
</script>

<main class="w-full h-full flex flex-col px-4">
    <div class="pt-4 pb-4" use:observe>
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
                        <DropdownMenu.Item>
                            <Settings/>
                            Роли
                        </DropdownMenu.Item>
                        <DropdownMenu.Item>
                            <Checkbox checked={true}/>
                            Сначала Избранные
                        </DropdownMenu.Item>
                        <DropdownMenu.Item>
                            <Checkbox/>
                            Заполненные
                        </DropdownMenu.Item>
                    </DropdownMenu.Content>
                </DropdownMenu.Root>
            </InputGroup.Addon>
        </InputGroup.Root>
    </div>
    <!-- TODO: добавить кнопку вернуться наверх-->
    <div class="grid grid-cols-1 gap-4 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5 2xl:grid-cols-6">
        <SongCard
            title="Когда-нибудь 2"
            artist="KSB muzic"
            description="Лучшая песня всея альбома 2026 давайте сыграем пожалуйста очень длинное описание"
            imageUrl="https://avatars.yandex.net/get-music-content/19035207/89bd1de5.a.43279081-1/m1000x1000"
            filledAssignments={1}
            totalAssignments={5}
        />

        <SongCard
            title="счастливый человек"
            artist="кис-кис"
            imageUrl="https://avatars.yandex.net/get-music-content/16469857/21036ce0.a.38430333-1/m1000x1000"
            featured={true}
            filledAssignments={1}
            totalAssignments={4}
        />

        <SongCard
            title="I Really Want to Stay at Your House"
            artist="Rosa Walton"
            imageUrl="https://avatars.yandex.net/get-music-content/17681324/a0a99b82.a.39716308-1/m1000x1000"
        />
    </div>
    {#if showScrollTop}
        <Button
            class="fixed left-4 bottom-18 z-50 rounded-full shadow-lg"
            size="icon"
            onclick={scrollToTop}
            aria-label="Вернуться наверх"
        >
            <ArrowUp/>
        </Button>
    {/if}
    <Button
        class="fixed right-4 bottom-18 z-50 rounded-full shadow-lg"
        size="icon"
        onclick={scrollToTop}
        aria-label="Вернуться наверх"
    >
        <Plus/>
    </Button>
</main>
