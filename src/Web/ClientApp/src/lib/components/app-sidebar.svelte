<script lang="ts" module>
    import type {AppTypes} from "$app/types";
    import BookOpenIcon from "@lucide/svelte/icons/book-open";
    import Settings2Icon from "@lucide/svelte/icons/settings-2";
    import {CalendarDaysIcon, MusicIcon, UserIcon} from "@lucide/svelte";

    type Pathname = ReturnType<AppTypes["Pathname"]>;
    type NavUrl = "#" | Pathname | `http${string}`;
    type NavItem = {
        title: string;
        url: NavUrl;
        onSelect?: () => void;
    };
    type NavMainItem = NavItem & {
        // This should be `Component` after @lucide/svelte updates types
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        icon: any;
        isActive?: boolean;
        items?: NavItem[];
    };
    type SidebarUser = {
        name: string;
        email: string;
        avatar?: string | null;
    };
    type SidebarData = {
        user: SidebarUser;
        navMain: NavMainItem[];
        navSecondary: NavSecondaryItem[];
    };
    type NavSecondaryItem = NavItem & {
        // This should be `Component` after @lucide/svelte updates types
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        icon: any;
    };

    export const defaultAppSidebarData: SidebarData = {
        user: {
            name: "",
            email: "potanin@nornickel.ru",
            avatar: undefined,
        },
        navMain: [
            {
                title: "Песни",
                url: "/app/songs",
                icon: MusicIcon,
                isActive: true,
            },
            {
                title: "Календарь",
                url: "/app/calendar",
                icon: CalendarDaysIcon,
            },
            {
                title: "Профиль",
                url: "/app/profile",
                icon: UserIcon,
            },
            {
                title: "Документация",
                url: "https://github.com/pxc1984/nnkl",
                icon: BookOpenIcon,
                items: [
                    {
                        title: "Техническое задание",
                        url: "https://nornickel-ai-hackathon.ru/task-2",
                    },
                    {
                        title: "Быстрый старт",
                        url: "#",
                    },
                    {
                        title: "VCS",
                        url: "https://github.com/pxc1984/nnkl",
                    },
                ],
            },
            {
                title: "Настройки",
                url: "#",
                icon: Settings2Icon,
                items: [
                    {
                        title: "Общие",
                        url: "#",
                    },
                    {
                        title: "Переключить тему",
                        url: "#",
                        onSelect: () => {
                        },
                    },
                ],
            },
        ],
        navSecondary: [
            // {
            // 	title: "Поддержка",
            // 	url: "#",
            // 	icon: LifeBuoyIcon,
            // },
            // {
            // 	title: "Обратная связь",
            // 	url: "#",
            // 	icon: SendIcon,
            // },
        ],
    };

    export type {NavUrl};
    export type {SidebarData, SidebarUser};
</script>

<script lang="ts">
    import type {UserProfile} from "$lib/auth/types";
    import {onMount} from "svelte";
    import NavMain from "./nav-main.svelte";
    import NavSecondary from "./nav-secondary.svelte";
    import NavUser from "./nav-user.svelte";
    import * as Sidebar from "$lib/components/ui/sidebar";
    import type {ComponentProps} from "svelte";

    let {
        ref = $bindable(null),
        currentUser = null,
        appSidebarData = defaultAppSidebarData,
        ...restProps
    }: ComponentProps<typeof Sidebar.Root> & {
        currentUser?: UserProfile | null;
        appSidebarData?: SidebarData;
    } = $props();

    const sidebarUser = $derived(
        currentUser
            ? {
                name: currentUser.name?.trim() || currentUser.email,
                email: currentUser.email,
                avatar: currentUser.avatarUrl ?? undefined,
            }
            : appSidebarData.user,
    );

    const THEME_STORAGE_KEY = "theme";

    const applyTheme = (theme: "dark" | "light") => {
        document.documentElement.classList.toggle("dark", theme === "dark");
        window.localStorage.setItem(THEME_STORAGE_KEY, theme);
    };

    const toggleTheme = () => {
        const nextTheme = document.documentElement.classList.contains("dark") ? "light" : "dark";
        applyTheme(nextTheme);
    };

    onMount(() => {
        const savedTheme = window.localStorage.getItem(THEME_STORAGE_KEY);
        applyTheme(savedTheme === "light" ? "light" : "dark");
    });

    const navMainItems = $derived.by(() =>
        appSidebarData.navMain.map((item) => {
            if (item.title !== "Настройки") {
                return item;
            }

            return {
                ...item,
                items: item.items?.map((subItem) =>
                    subItem.title === "Переключить тему"
                        ? {
                            ...subItem,
                            onSelect: toggleTheme,
                        }
                        : subItem,
                ),
            };
        }),
    );
</script>

<Sidebar.Root bind:ref variant="inset" {...restProps}>
    <Sidebar.Header>
        <Sidebar.Menu>
            <Sidebar.MenuItem>
                <Sidebar.MenuButton size="lg">
                    {#snippet child()}
                        <a href="https://nornickel.ru" class="flex items-center gap-2 font-medium">
                            <div class="z-logo header__logo header__logo--big z-logo--full-image"><img
                                src="https://nornickel.ru/images/logo/logo-inverted-ru.svg" alt="logo"
                                class="z-logo__img">
                            </div>
                        </a>
                    {/snippet}
                </Sidebar.MenuButton>
            </Sidebar.MenuItem>
        </Sidebar.Menu>
    </Sidebar.Header>
    <Sidebar.Content class="overflow-hidden">
        <NavMain items={navMainItems}/>
        <NavSecondary items={appSidebarData.navSecondary} class="mt-auto"/>
    </Sidebar.Content>
    <Sidebar.Footer>
        <NavUser user={sidebarUser}/>
    </Sidebar.Footer>
</Sidebar.Root>
