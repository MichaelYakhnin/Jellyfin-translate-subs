import { useState, useEffect, useCallback } from 'react';
import { MediaCard } from './components/MediaCard';
import type { MediaItemWithStatus, Library, Language } from './types';
import { fetchMedia, fetchLibraries, translateMedia, batchTranslate, fetchLanguages } from './services/api';

const DEFAULT_LANGUAGE = 'rus';

function App() {
  const [media, setMedia] = useState<MediaItemWithStatus[]>([]);
  const [libraries, setLibraries] = useState<Library[]>([]);
  const [languages, setLanguages] = useState<Language[]>([
    { code: 'rus', name: 'Russian' },
    { code: 'heb', name: 'Hebrew' }
  ]);
  const [defaultLanguage] = useState(DEFAULT_LANGUAGE);
  const [batchLanguage, setBatchLanguage] = useState(DEFAULT_LANGUAGE);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const [filter, setFilter] = useState<'all' | 'movies' | 'episodes'>('all');
  const [selectedLibrary, setSelectedLibrary] = useState<string>('');

  const loadMedia = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);
      const type = filter === 'all' ? undefined : filter;
      const items = await fetchMedia(type, selectedLibrary || undefined);
      setMedia(items.map(item => ({ ...item, translationStatus: 'idle' as const })));
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load media');
    } finally {
      setLoading(false);
    }
  }, [filter, selectedLibrary]);

  const loadLibraries = useCallback(async () => {
    try {
      const libs = await fetchLibraries();
      setLibraries(libs);
    } catch (err) {
      console.error('Failed to load libraries:', err);
    }
  }, []);

  const loadLanguages = useCallback(async () => {
    try {
      const langs = await fetchLanguages();
      if (langs.length > 0) {
        setLanguages(langs);
      }
    } catch (err) {
      console.error('Failed to load languages:', err);
    }
  }, []);

  useEffect(() => {
    loadMedia();
  }, [loadMedia]);

  useEffect(() => {
    loadLibraries();
    loadLanguages();
  }, [loadLibraries, loadLanguages]);

  const handleTranslate = async (path: string, targetLanguage: string, subtitlePath?: string) => {
    setMedia(prev => prev.map(m => 
      m.path === path 
        ? { ...m, translationStatus: 'translating' as const, translationMessage: undefined }
        : m
    ));

    try {
      const result = await translateMedia(path, targetLanguage, subtitlePath);
      setMedia(prev => prev.map(m => 
        m.path === path 
          ? { 
              ...m, 
              translationStatus: result.success ? 'done' as const : 'error' as const,
              translationMessage: result.message
            }
          : m
      ));
    } catch (err) {
      setMedia(prev => prev.map(m => 
        m.path === path 
          ? { ...m, translationStatus: 'error' as const, translationMessage: err instanceof Error ? err.message : 'Unknown error' }
          : m
      ));
    }
  };

  const handleBatchTranslate = async () => {
    const selectedItems = media.filter(m => selectedIds.has(m.id) && m.hasSubtitles);
    if (selectedItems.length === 0) return;

    setMedia(prev => prev.map(m => 
      selectedIds.has(m.id)
        ? { ...m, translationStatus: 'translating' as const, translationMessage: undefined }
        : m
    ));

    try {
      const items = selectedItems.map(m => ({ mediaPath: m.path }));
      await batchTranslate(items, batchLanguage);
      
      setMedia(prev => prev.map(m => 
        selectedIds.has(m.id)
          ? { ...m, translationStatus: 'done' as const, translationMessage: 'Batch translation complete' }
          : m
      ));
    } catch (err) {
      setMedia(prev => prev.map(m => 
        selectedIds.has(m.id)
          ? { ...m, translationStatus: 'error' as const, translationMessage: err instanceof Error ? err.message : 'Unknown error' }
          : m
      ));
    }
  };

  const handleSelect = (id: string) => {
    setSelectedIds(prev => {
      const next = new Set(prev);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  };

  const handleSelectAll = () => {
    if (selectedIds.size === media.length) {
      setSelectedIds(new Set());
    } else {
      setSelectedIds(new Set(media.map(m => m.id)));
    }
  };

  const selectedCount = selectedIds.size;
  const selectedWithSubtitles = media.filter(m => selectedIds.has(m.id) && m.hasSubtitles).length;

  return (
    <div className="min-h-screen bg-gray-50 dark:bg-gray-900">
      <header className="bg-white dark:bg-gray-800 shadow-sm border-b border-gray-200 dark:border-gray-700">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-4">
          <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
            <div>
              <h1 className="text-2xl font-bold text-gray-900 dark:text-white">
                Subtitle Translator
              </h1>
              <p className="text-sm text-gray-500 dark:text-gray-400 mt-1">
                Translate subtitles using LibreTranslate
              </p>
            </div>

            <div className="flex flex-wrap items-center gap-3">
              <select
                value={selectedLibrary}
                onChange={(e) => setSelectedLibrary(e.target.value)}
                className="px-3 py-2 bg-white dark:bg-gray-700 border border-gray-300 dark:border-gray-600 rounded-md text-sm text-gray-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-purple-500"
              >
                <option value="">All Libraries</option>
                {libraries.map(lib => (
                  <option key={lib.id} value={lib.id}>{lib.name}</option>
                ))}
              </select>

              <div className="flex rounded-md overflow-hidden border border-gray-300 dark:border-gray-600">
                {(['all', 'movies', 'episodes'] as const).map(f => (
                  <button
                    key={f}
                    onClick={() => setFilter(f)}
                    className={`px-3 py-2 text-sm font-medium transition-colors ${
                      filter === f
                        ? 'bg-purple-600 text-white'
                        : 'bg-white dark:bg-gray-700 text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-600'
                    }`}
                  >
                    {f.charAt(0).toUpperCase() + f.slice(1)}
                  </button>
                ))}
              </div>

              <button
                onClick={loadMedia}
                disabled={loading}
                className="px-3 py-2 bg-gray-100 dark:bg-gray-700 text-gray-700 dark:text-gray-300 rounded-md text-sm font-medium hover:bg-gray-200 dark:hover:bg-gray-600 transition-colors disabled:opacity-50"
              >
                {loading ? 'Loading...' : 'Refresh'}
              </button>
            </div>
          </div>
        </div>
      </header>

      {selectedCount > 0 && (
        <div className="bg-purple-600 text-white shadow-md">
          <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-3">
            <div className="flex flex-wrap items-center justify-between gap-3">
              <span className="text-sm font-medium">
                {selectedCount} selected ({selectedWithSubtitles} with subtitles)
              </span>
              <div className="flex flex-wrap items-center gap-2">
                <span className="text-sm">Translate to:</span>
                <select
                  value={batchLanguage}
                  onChange={(e) => setBatchLanguage(e.target.value)}
                  className="px-2 py-1 bg-purple-700 text-white rounded text-sm border border-purple-500"
                >
                  {languages.map(lang => (
                    <option key={lang.code} value={lang.code}>{lang.name}</option>
                  ))}
                </select>
                <button
                  onClick={() => setSelectedIds(new Set())}
                  className="px-3 py-1.5 text-sm font-medium bg-purple-700 rounded-md hover:bg-purple-800 transition-colors"
                >
                  Clear Selection
                </button>
                <button
                  onClick={handleBatchTranslate}
                  disabled={selectedWithSubtitles === 0}
                  className="px-3 py-1.5 text-sm font-medium bg-white text-purple-600 rounded-md hover:bg-gray-100 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
                >
                  Translate Selected
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

      <main className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        {error && (
          <div className="mb-6 p-4 bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 rounded-lg">
            <p className="text-red-700 dark:text-red-400">{error}</p>
          </div>
        )}

        {loading ? (
          <div className="flex items-center justify-center py-12">
            <svg className="animate-spin h-8 w-8 text-purple-600" fill="none" viewBox="0 0 24 24">
              <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
              <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z" />
            </svg>
          </div>
        ) : media.length === 0 ? (
          <div className="text-center py-12">
            <p className="text-gray-500 dark:text-gray-400">No media items found</p>
            <p className="text-sm text-gray-400 dark:text-gray-500 mt-2">
              Make sure Jellyfin is configured and has media libraries
            </p>
          </div>
        ) : (
          <>
            <div className="mb-4 flex items-center gap-4">
              <input
                type="checkbox"
                checked={selectedIds.size === media.length && media.length > 0}
                onChange={handleSelectAll}
                className="h-4 w-4 text-purple-600 rounded border-gray-300 focus:ring-purple-500"
              />
              <span className="text-sm text-gray-600 dark:text-gray-400">
                Select all ({media.length} items)
              </span>
            </div>

            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-6">
              {media.map(item => (
                <MediaCard
                  key={item.id}
                  item={item}
                  languages={languages}
                  defaultLanguage={defaultLanguage}
                  onTranslate={handleTranslate}
                  isSelected={selectedIds.has(item.id)}
                  onSelect={handleSelect}
                />
              ))}
            </div>
          </>
        )}
      </main>

      <footer className="bg-white dark:bg-gray-800 border-t border-gray-200 dark:border-gray-700 py-4">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 text-center text-sm text-gray-500 dark:text-gray-400">
          Jellyfin Subtitle Translator
        </div>
      </footer>
    </div>
  );
}

export default App;
