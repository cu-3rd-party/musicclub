import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment.development';

export interface DataEntry {
    id: string;
    fileName: string;
    contentType: string;
    size: number;
    url: string;
    createdAt: string;
}

export interface CreateDataPayload {
    file: File;
}

@Injectable({
    providedIn: 'root'
})
export class DataService {
    private readonly http = inject(HttpClient);
    private readonly apiUrl = environment.API_URL;

    uploadFile(file: File): Observable<DataEntry> {
        const formData = new FormData();
        formData.append('file', file);
        return this.http.post<DataEntry>(`${this.apiUrl}/api/v1/data`, formData);
    }

    getFile(fileId: string): Observable<DataEntry> {
        return this.http.get<DataEntry>(`${this.apiUrl}/api/v1/data/${fileId}`);
    }

    deleteFile(fileId: string): Observable<void> {
        return this.http.delete<void>(`${this.apiUrl}/api/v1/data/${fileId}`);
    }
}
