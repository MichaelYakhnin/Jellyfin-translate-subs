export interface MediaItem {
  id: string;
  name: string;
  type: string;
  path: string;
  tags: string[];
  overview: string;
  productionYear: number | null;
  premiereDate: string | null;
  subtitlePaths: string[];
  hasSubtitles: boolean;
}

export interface Library {
  id: string;
  name: string;
  type: string;
}

export interface TranslationResult {
  success: boolean;
  message: string;
  path: string;
  translatedFiles: {
    source: string;
    output: string;
    entries: number;
  }[];
  errors: string[];
}

export type TranslationStatus = 'idle' | 'translating' | 'done' | 'error';

export interface MediaItemWithStatus extends MediaItem {
  translationStatus: TranslationStatus;
  translationMessage?: string;
}
