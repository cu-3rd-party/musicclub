<script lang="ts">
    import * as InputGroup from "$lib/components/ui/input-group";
    import * as DropdownMenu from "$lib/components/ui/dropdown-menu";
    import {
        ArrowUp,
        Ellipsis,
        SearchIcon,
        X,
        Funnel
    } from "@lucide/svelte";
    import {Checkbox} from "$lib/components/ui/checkbox";
    import SongCard from "$lib/components/songs/song-card.svelte";
    import {Button} from "$lib/components/ui/button";
    import {getSongs} from "$lib/api/songs";
    import type {Song} from "$lib/songs/types";
    import {page} from "$app/state";
    import {goto} from "$app/navigation";
    import TextType from "$lib/components/songs/text-type.svelte";
    import * as Dialog from "$lib/components/ui/dialog";
    import CreateSong from "$lib/components/create-song.svelte";

    let showScrollTop = $state(false);

    let searchInput = $state("");
    let songs = $state<Song[]>([]);

    let loading = $state(true);
    let loadingMore = $state(false);
    let error = $state<string | null>(null);

    let nextPageToken = $state<string | null>(null);

    let favoriteFirst = $state(false);
    let showFull = $state(false);
    let rolesDialogOpen = $state(false);
    let selectedRoleTitles = $state<Set<string>>(new Set());

    const searchQuery = $derived(
        page.url.searchParams.get("q") ?? ""
    );

    const allRoleTitles = $derived(
        Array.from(
            new Set(
                songs.flatMap((song) =>
                    song.roles.map((role) => role.title)
                )
            )
        ).sort()
    );

    const filteredSongs = $derived(
        (() => {
            let result = [...songs];

            if (favoriteFirst) {
                result.sort((a, b) =>
                    b.featured === a.featured
                        ? 0
                        : b.featured
                            ? 1
                            : -1
                );
            }

            if (!showFull) {
                result = result.filter(
                    (song) =>
                        song.roles.length === 0 ||
                        song.roles.some(
                            (role) => role.assignment === null
                        )
                );
            }

            if (selectedRoleTitles.size > 0) {
                result = result.filter((song) =>
                    song.roles.some(
                        (role) =>
                            selectedRoleTitles.has(role.title) &&
                            role.assignment === null
                    )
                );
            }

            return result;
        })()
    );

    function updateFilterUrl(
        updates: {
            q?: string | null;
            roles?: Set<string> | null;
            favoriteFirst?: boolean | null;
            showFull?: boolean | null;
        }
    ) {
        const url = new URL(page.url);

        if ("q" in updates) {
            const query = updates.q?.trim() ?? "";

            if (query) {
                url.searchParams.set("q", query);
            } else {
                url.searchParams.delete("q");
            }
        }

        if ("roles" in updates) {
            url.searchParams.delete("roles");

            if (updates.roles && updates.roles.size > 0) {
                for (const role of updates.roles) {
                    url.searchParams.append("roles", role);
                }
            }
        }

        if ("favoriteFirst" in updates) {
            if (updates.favoriteFirst) {
                url.searchParams.set("favoriteFirst", "1");
            } else {
                url.searchParams.delete("favoriteFirst");
            }
        }

        if ("showFull" in updates) {
            if (updates.showFull) {
                url.searchParams.set("showFull", "1");
            } else {
                url.searchParams.delete("showFull");
            }
        }

        goto(`${url.pathname}${url.search}`, {
            replaceState: true,
            noScroll: true,
            keepFocus: true
        });
    }

    function toggleRoleTitle(title: string, checked: boolean) {
        const newSet = new Set(selectedRoleTitles);

        if (checked) {
            newSet.add(title);
        } else {
            newSet.delete(title);
        }

        selectedRoleTitles = newSet;

        updateFilterUrl({
            roles: newSet
        });
    }

    function clearRoleFilters() {
        selectedRoleTitles = new Set();

        updateFilterUrl({
            roles: new Set()
        });
    }

    function hasActiveRoleFilters() {
        return selectedRoleTitles.size > 0;
    }

    function setFavoriteFirst(value: boolean) {
        favoriteFirst = value;

        updateFilterUrl({
            favoriteFirst: value
        });
    }

    function setShowFull(value: boolean) {
        showFull = value;

        updateFilterUrl({
            showFull: value
        });
    }

    function observe(element: HTMLElement) {
        const scrollContainer =
            document.getElementById("app-container");

        const observer = new IntersectionObserver(
            ([entry]) => {
                showScrollTop = !entry.isIntersecting;
            },
            {
                root: scrollContainer,
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
        document.getElementById("app-container")?.scrollTo({
            top: 0,
            behavior: "smooth"
        });
    }

    /*
     * Sentinel для infinite scroll.
     *
     * Когда он появляется в пределах scroll-контейнера,
     * загружаем следующую страницу.
     */
    function observeLoadMore(element: HTMLElement) {
        const scrollContainer =
            document.getElementById("app-container");

        const observer = new IntersectionObserver(
            ([entry]) => {
                if (entry.isIntersecting) {
                    loadMoreSongs();
                }
            },
            {
                root: scrollContainer,
                threshold: 0,
                rootMargin: "800px"
            }
        );

        observer.observe(element);

        return {
            destroy() {
                observer.disconnect();
            }
        };
    }

    async function loadMoreSongs() {
        if (
            loading ||
            loadingMore ||
            !nextPageToken
        ) {
            return;
        }

        loadingMore = true;

        try {
            const result = await getSongs({
                query: searchQuery || undefined,
                pageSize: 24,
                pageToken: nextPageToken
            });

            const existingIds = new Set(songs.map((song) => song.id));

            const newSongs = result.songs.filter(
                (song) => !existingIds.has(song.id)
            );

            songs = [...songs, ...newSongs];
            nextPageToken = result.nextPageToken;
        } catch (err) {
            console.error("Не удалось загрузить следующую страницу", err);
        } finally {
            loadingMore = false;
        }
    }

    let searchTimer: ReturnType<typeof setTimeout>;

    function handleSearchInput(value: string) {
        searchInput = value;

        clearTimeout(searchTimer);

        searchTimer = setTimeout(() => {
            updateFilterUrl({
                q: value
            });
        }, 300);
    }

    // Восстанавливаем фильтры из URL.
    $effect(() => {
        const params = page.url.searchParams;

        searchInput = params.get("q") ?? "";

        const roles = params.getAll("roles");
        selectedRoleTitles = new Set(roles);

        favoriteFirst =
            params.get("favoriteFirst") === "1";

        showFull =
            params.get("showFull") === "1";
    });

    /*
     * Загружаем первую страницу.
     *
     * Этот effect зависит от searchQuery, поэтому при изменении
     * поискового запроса список сбрасывается и начинается заново.
     */
    $effect(() => {
        const query = searchQuery;

        let cancelled = false;

        async function loadSongs() {
            loading = true;
            error = null;

            // Сбрасываем pagination перед новой первой страницей.
            songs = [];
            nextPageToken = null;

            try {
                const result = await getSongs({
                    query: query || undefined,
                    pageSize: 24
                });

                if (!cancelled) {
                    songs = result.songs;
                    nextPageToken = result.nextPageToken;
                }
            } catch (err) {
                if (!cancelled) {
                    error = "Не удалось загрузить песни";
                    console.error(err);
                }
            } finally {
                if (!cancelled) {
                    loading = false;
                }
            }
        }

        loadSongs();

        return () => {
            cancelled = true;
        };
    });
</script>

<main class="w-full h-full flex flex-col px-4">
    <div class="pt-4 pb-4" use:observe>
        <InputGroup.Root>
            <InputGroup.Addon>
                <SearchIcon/>
            </InputGroup.Addon>

            <InputGroup.Input
                placeholder="Название песни"
                value={searchInput}
                oninput={(event) =>
					handleSearchInput(
						event.currentTarget.value
					)}
            />

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

                    <DropdownMenu.Content
                        align="end"
                        class="w-56"
                    >
                        <DropdownMenu.Item
                            class="justify-between"
                            onclick={(e) => e.preventDefault()}
                        >
                            <button
                                type="button"
                                class="flex flex-1 items-center gap-1.5"
                                onclick={(e) => {
									e.stopPropagation();
									rolesDialogOpen = true;
								}}
                            >
                                <Funnel/>

                                <span>
									Свободные роли
								</span>

                                {#if selectedRoleTitles.size !== 0}
									<span>
										({selectedRoleTitles.size})
									</span>
                                {/if}
                            </button>

                            {#if hasActiveRoleFilters()}
                                <button
                                    type="button"
                                    class="ml-2 flex size-5 items-center justify-center rounded-sm hover:bg-muted"
                                    onclick={(e) => {
										e.stopPropagation();
										clearRoleFilters();
									}}
                                    aria-label="Очистить фильтр ролей"
                                >
                                    <X class="size-4"/>
                                </button>
                            {/if}
                        </DropdownMenu.Item>

                        <DropdownMenu.Item
                            onclick={() =>
								setFavoriteFirst(
									!favoriteFirst
								)}
                        >
                            <Checkbox
                                checked={favoriteFirst}
                            />

                            <span class="text-sm">
								Сначала избранные
							</span>
                        </DropdownMenu.Item>

                        <DropdownMenu.Item
                            onclick={() =>
								setShowFull(!showFull)}
                        >
                            <Checkbox
                                checked={showFull}
                            />

                            <span class="text-sm">
								Показывать заполненные
							</span>
                        </DropdownMenu.Item>
                    </DropdownMenu.Content>
                </DropdownMenu.Root>
            </InputGroup.Addon>
        </InputGroup.Root>
    </div>

    {#if loading}
        <div class="py-8 text-center text-muted-foreground">
            Загрузка...
        </div>
    {:else if error}
        <div class="py-8 text-center text-destructive">
            {error}
        </div>
    {:else if songs.length === 0}
        <div class="py-8 text-center text-muted-foreground">
            <TextType
                text={
					searchQuery
						? "404 NOT FOUND"
						: "204 NO CONTENT "
				}
                typingSpeed={100}
                deletingSpeed={50}
                showCursor={true}
                loop={false}
                cursorCharacter="▎"
                cursorBlinkDuration={0.5}
                variableSpeed={{
					min: 60,
					max: 120
				}}
            />
        </div>
    {:else}
        <div
            class="grid grid-cols-1 gap-4 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5 2xl:grid-cols-6"
        >
            {#each filteredSongs as song (song.id)}
                <SongCard
                    songId={song.id}
                    title={song.title}
                    artist={song.artist}
                    description={
						song.description ?? undefined
					}
                    imageUrl={
						song.thumbnailUrl ?? undefined
					}
                    featured={song.featured}
                    filledAssignments={song.roles.filter(
						(role) =>
							role.assignment !== null
					).length}
                    totalAssignments={song.roles.length}
                />
            {/each}
        </div>

        <!--
            Sentinel находится после списка.
            rootMargin 400px означает, что следующая страница
            начнёт грузиться ещё до того, как пользователь
            достигнет самого низа.
        -->
        <div
            class="h-1 w-full shrink-0"
            use:observeLoadMore
            aria-hidden="true"
        ></div>

        {#if loadingMore}
            <div class="py-6 text-center text-sm text-muted-foreground">
                Загрузка...
            </div>
        {/if}
    {/if}

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

    <CreateSong
        class="fixed right-4 bottom-18 z-50 shadow-lg"
    />

    <Dialog.Root bind:open={rolesDialogOpen}>
        <Dialog.Portal>
            <Dialog.Content>
                <Dialog.Header>
                    <Dialog.Title class="text-lg font-semibold">
                        Фильтр по ролям
                    </Dialog.Title>

                    <Dialog.Description
                        class="text-sm text-muted-foreground"
                    >
                        Выберите роли, которые должны быть
                        свободны. Будут показаны песни, где
                        выбранные роли не заняты.
                    </Dialog.Description>
                </Dialog.Header>

                <div
                    class="grid gap-2 max-h-80 overflow-y-auto"
                >
                    {#each allRoleTitles as title}
                        <label
                            class="flex items-center gap-2 cursor-pointer"
                        >
                            <Checkbox
                                checked={selectedRoleTitles.has(
									title
								)}
                                onCheckedChange={(checked) =>
									toggleRoleTitle(
										title,
										checked
									)}
                            />

                            <span class="text-sm">
								{title}
							</span>
                        </label>
                    {/each}

                    {#if allRoleTitles.length === 0}
                        <p
                            class="text-sm text-muted-foreground text-center py-4"
                        >
                            Ролей пока нет
                        </p>
                    {/if}
                </div>

                <Dialog.Footer>
                    <Button
                        type="button"
                        onclick={() =>
							(rolesDialogOpen = false)}
                    >
                        Готово
                    </Button>
                </Dialog.Footer>
            </Dialog.Content>
        </Dialog.Portal>
    </Dialog.Root>
</main>
