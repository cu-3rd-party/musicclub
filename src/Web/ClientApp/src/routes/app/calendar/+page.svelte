<script lang="ts">
    import Timetable from "$lib/components/timetable/timetable.svelte";
    import type {TimetableEvent} from "$lib/timetable/types";
    import {Button} from "$lib/components/ui/button";
    import {Plus} from "@lucide/svelte";

    const events: TimetableEvent[] = [
        {
            id: "1",
            title: "Когда-нибудь 2",
            start: 9 * 60,
            end: 10 * 60 + 30
        },
        {
            id: "2",
            title: "счастливый человек",
            start: 12 * 60,
            end: 13 * 60
        },
        {
            id: "3",
            title: "I Really Want to Stay at Your House",
            start: 15 * 60 + 30,
            end: 17 * 60
        }
    ];
</script>

<Timetable
    events={events}
    startHour={8}
    endHour={23}
    hourHeight={80}
/>
<!--TODO: добавить кнопку Сегодня которая показывается если переключиться на другие дни от сегодня и переводит на сегодня-->
<Button
    class="fixed right-4 bottom-18 z-50 rounded-full shadow-lg"
    size="icon"
    aria-label="Вернуться наверх"
>
    <!--    onclick={openCreateSongDialog}-->
    <Plus/>
</Button>
