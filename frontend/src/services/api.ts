import type { MediaItem, Library, TranslationResult, Language } from '../types';

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

export async function translateMedia(
  mediaPath: string, 
  targetLanguage?: string, 
  subtitlePath?: string
): Promise<TranslationResult> {
  const response = await fetch(`${API_BASE}/translate/manual`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ 
      mediaPath,
      targetLanguage,
      subtitlePath
    }),
  });
  
  if (!response.ok) throw new Error('Failed to translate');
  return response.json();
}

export async function batchTranslate(
  items: { mediaPath: string; subtitlePath?: string }[],
  targetLanguage?: string
): Promise<{ total: number; targetLanguage: string; results: unknown[] }> {
  const response = await fetch(`${API_BASE}/translate/batch`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ 
      items,
      targetLanguage
    }),
  });
  
  if (!response.ok) throw new Error('Failed to batch translate');
  return response.json();
}

export async function discoverSubtitles(path: string): Promise<{ path: string; subtitlesFound: number; subtitles: string[] }> {
  const response = await fetch(`${API_BASE}/translate/discover?path=${encodeURIComponent(path)}`);
  if (!response.ok) throw new Error('Failed to discover subtitles');
  return response.json();
}

export async function fetchLanguages(): Promise<Language[]> {
  const response = await fetch(`${API_BASE}/translate/languages`);
  if (!response.ok) throw new Error('Failed to fetch languages');
  
  const data = await response.json();
  return data.languages;
}

export async function checkHealth(): Promise<{ status: string }> {
  const response = await fetch(`${API_BASE}/translate`);
  if (!response.ok) throw new Error('Service unavailable');
  return response.json();
}
