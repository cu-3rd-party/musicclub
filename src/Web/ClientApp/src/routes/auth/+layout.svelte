<script lang="ts">
    import type { LayoutProps } from './$types';

    let { data, children }: LayoutProps = $props();
</script>

<div class="grid min-h-svh lg:grid-cols-2">
    <div class="flex flex-col gap-4 p-6 md:p-10">
<!--        <div class="flex justify-center gap-2 md:justify-start">-->
<!--            <a href="https://nornickel.ru" class="flex items-center gap-2 font-medium">-->
<!--                <div class="z-logo header__logo header__logo&#45;&#45;big z-logo&#45;&#45;full-image"><img-->
<!--                        src="https://nornickel.ru/images/logo/logo-inverted-ru.svg" alt="logo" class="z-logo__img">-->
<!--                </div>-->
<!--            </a>-->
<!--        </div>-->
        <div class="flex flex-1 items-center justify-center">
            <div class="w-full max-w-xs">
                {@render children()}
            </div>
        </div>
    </div>
    <div class="bg-muted relative hidden lg:block">
        <img
                src={data.bgImage}
                alt=""
                class="absolute inset-0 h-full w-full object-cover dark:brightness-[0.2] dark:grayscale"
        />
    </div>
</div>
