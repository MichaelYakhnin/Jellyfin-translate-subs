import type { MediaItem, Library, TranslationResult } from '../types';

const API_BASE = '/api';

export async function fetchMedia(type?: 'movies' | 'episodes', libraryId?: string): Promise<MediaItem[]> {
  const params = new URLSearchParams();
  if (type) params.set('type', type);
  if (libraryId) params.set('libraryId', libraryId);
  
  const response = await fetch(`${API_BASE}/media?${params}`);
  if (!response.ok) throw new Error('Failed to fetch media');
  
  const data = await response.json();
  return data.items;
}

export async function fetchLibraries(): Promise<Library[]> {
  const response = await fetch(`${API_BASE}/media/libraries`);
  if (!response.ok) throw new Error('Failed to fetch libraries');
  
  const data = await response.json();
  return data.libraries;
}

export async function translateMedia(path: string): Promise<TranslationResult> {
  const response = await fetch(`${API_BASE}/translate/manual`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ path }),
  });
  
  if (!response.ok) throw new Error('Failed to translate');
  return response.json();
}

export async function batchTranslate(paths: string[]): Promise<{ total: number; results: unknown[] }> {
  const response = await fetch(`${API_BASE}/translate/batch`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ paths }),
  });
  
  if (!response.ok) throw new Error('Failed to batch translate');
  return response.json();
}

export async function discoverSubtitles(path: string): Promise<{ path: string; subtitlesFound: number; subtitles: string[] }> {
  const response = await fetch(`${API_BASE}/translate/discover?path=${encodeURIComponent(path)}`);
  if (!response.ok) throw new Error('Failed to discover subtitles');
  return response.json();
}

export async function checkHealth(): Promise<{ status: string }> {
  const response = await fetch(`${API_BASE}/translate`);
  if (!response.ok) throw new Error('Service unavailable');
  return response.json();
}
