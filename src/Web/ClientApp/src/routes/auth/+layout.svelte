<script lang="ts">
    import type {LayoutProps} from './$types';

    let {data, children}: LayoutProps = $props();
</script>

<div class="relative flex min-h-svh items-center justify-center overflow-hidden p-6 md:p-10">
    <img
        src={data.bgImage}
        alt=""
        class="absolute inset-0 h-full w-full object-cover dark:brightness-[0.2] dark:grayscale"
    />
    <div class="relative z-10 w-full max-w-xs">
        {@render children()}
    </div>
</div>
