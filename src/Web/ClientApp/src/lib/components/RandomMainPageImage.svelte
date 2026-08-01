<script lang="ts">
    const imagesPool = [
        "https://nornickel.ru/images/main-page/china.webp",
        "https://nornickel.ru/images/main-page/owl.jpg",
        "https://nornickel.ru/images/biodiversity/save-ecosystem.jpg",
    ];

    const randomIndex = Math.floor(Math.random() * imagesPool.length);
    let currentImage = imagesPool[randomIndex];
</script>

<img
        id="main-page-image"
        src={currentImage}
        alt="placeholder"
        class="absolute inset-0 h-full w-full object-cover dark:brightness-[0.2] dark:grayscale"
/>
