import { useState } from 'react';
import type { MediaItemWithStatus, TranslationStatus, Language } from '../types';

interface MediaCardProps {
  item: MediaItemWithStatus;
  languages: Language[];
  defaultLanguage: string;
  onTranslate: (mediaPath: string, targetLanguage: string, subtitlePath?: string) => void;
  isSelected: boolean;
  onSelect: (id: string) => void;
}

export function MediaCard({ item, languages, defaultLanguage, onTranslate, isSelected, onSelect }: MediaCardProps) {
  const [selectedSubtitle, setSelectedSubtitle] = useState<string>('');
  const [selectedLanguage, setSelectedLanguage] = useState(defaultLanguage);

  const statusStyles: Record<TranslationStatus, { bg: string; text: string; label: string }> = {
    idle: { bg: 'bg-gray-100 dark:bg-gray-800', text: 'text-gray-600 dark:text-gray-400', label: '' },
    translating: { bg: 'bg-yellow-100 dark:bg-yellow-900', text: 'text-yellow-700 dark:text-yellow-300', label: 'Translating...' },
    done: { bg: 'bg-green-100 dark:bg-green-900', text: 'text-green-700 dark:text-green-300', label: 'Done' },
    error: { bg: 'bg-red-100 dark:bg-red-900', text: 'text-red-700 dark:text-red-300', label: 'Error' },
  };

  const statusStyle = statusStyles[item.translationStatus];

  const handleTranslate = () => {
    onTranslate(item.path, selectedLanguage, selectedSubtitle || undefined);
  };

  return (
    <div
      className={`
        relative bg-white dark:bg-gray-800 rounded-lg shadow-md overflow-hidden
        border-2 transition-all
        ${isSelected ? 'border-purple-500 ring-2 ring-purple-200' : 'border-transparent'}
        hover:shadow-lg
      `}
    >
      <div className="p-4">
        <div className="flex items-start justify-between">
          <div className="flex-1 min-w-0">
            <h3 className="text-lg font-semibold text-gray-900 dark:text-white truncate">
              {item.name}
            </h3>
            <p className="text-sm text-gray-500 dark:text-gray-400">
              {item.type === 'Movie' ? 'Movie' : 'Episode'}
              {item.productionYear && ` • ${item.productionYear}`}
            </p>
          </div>
          
          <input
            type="checkbox"
            checked={isSelected}
            onChange={() => onSelect(item.id)}
            className="ml-3 h-5 w-5 text-purple-600 rounded border-gray-300 focus:ring-purple-500"
          />
        </div>

        {item.hasSubtitles ? (
          <div className="mt-3 space-y-2">
            <div className="flex items-center text-sm text-gray-600 dark:text-gray-400">
              <svg className="w-4 h-4 mr-1" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
              </svg>
              {item.subtitlePaths.length} subtitle(s) found
            </div>

            {item.subtitlePaths.length > 1 && (
              <select
                value={selectedSubtitle}
                onChange={(e) => setSelectedSubtitle(e.target.value)}
                className="w-full px-2 py-1 text-sm border border-gray-300 dark:border-gray-600 rounded bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
              >
                <option value="">All subtitles</option>
                {item.subtitlePaths.map((path, i) => (
                  <option key={i} value={path}>
                    {path.split('/').pop()?.replace('.srt', '') || `Subtitle ${i + 1}`}
                  </option>
                ))}
              </select>
            )}

            <div className="flex items-center space-x-2">
              <span className="text-sm text-gray-600 dark:text-gray-400">To:</span>
              <select
                value={selectedLanguage}
                onChange={(e) => setSelectedLanguage(e.target.value)}
                className="flex-1 px-2 py-1 text-sm border border-gray-300 dark:border-gray-600 rounded bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
              >
                {languages.map((lang) => (
                  <option key={lang.code} value={lang.code}>
                    {lang.name}
                  </option>
                ))}
              </select>
            </div>
          </div>
        ) : (
          <div className="mt-3 flex items-center text-sm text-gray-400 dark:text-gray-500">
            <svg className="w-4 h-4 mr-1" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
            </svg>
            No subtitles
          </div>
        )}

        {item.translationStatus !== 'idle' && (
          <div className={`mt-3 px-2 py-1 rounded text-sm ${statusStyle.bg} ${statusStyle.text}`}>
            {statusStyle.label}
            {item.translationMessage && (
              <span className="block text-xs mt-1 opacity-75">{item.translationMessage}</span>
            )}
          </div>
        )}
      </div>

      <div className="px-4 py-3 bg-gray-50 dark:bg-gray-700 border-t border-gray-100 dark:border-gray-600">
        <button
          onClick={handleTranslate}
          disabled={item.translationStatus === 'translating' || !item.hasSubtitles}
          className={`
            w-full py-2 px-4 rounded-md font-medium transition-all
            ${item.hasSubtitles && item.translationStatus !== 'translating'
              ? 'bg-purple-600 text-white hover:bg-purple-700 active:bg-purple-800'
              : 'bg-gray-300 text-gray-500 cursor-not-allowed dark:bg-gray-600'
            }
          `}
        >
          {item.translationStatus === 'translating' ? (
            <span className="flex items-center justify-center">
              <svg className="animate-spin -ml-1 mr-2 h-4 w-4 text-white" fill="none" viewBox="0 0 24 24">
                <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z" />
              </svg>
              Translating...
            </span>
          ) : (
            `Translate to ${languages.find(l => l.code === selectedLanguage)?.name || selectedLanguage}`
          )}
        </button>
      </div>
    </div>
  );
}
