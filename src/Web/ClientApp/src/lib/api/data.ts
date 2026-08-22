import {api} from "$lib/api/client";
import type {UUID} from "node:crypto"

export async function createData(
    file: File
): Promise<UUID> {
    const formData = new FormData();

    formData.append("file", file);

    const response = await api.post<UUID>(
        "/api/v1/data",
        formData,
    );

    return response.data;
}

export async function getData(
    dataId: UUID,
): Promise<Blob> {
    const response = await api.get<Blob>(
        `/api/v1/data/${dataId}`,
        {
            responseType: "blob",
        },
    );

    return response.data;
}
