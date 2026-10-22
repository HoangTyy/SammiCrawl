import { createContext, useContext, useState, useEffect } from 'react';
import type React from 'react';
import { translations } from '../i18n/translations';
import type { Language } from '../i18n/translations';

export type Theme = 'dark' | 'light';

interface AppContextType {
  theme: Theme;
  language: Language;
  toggleTheme: () => void;
  toggleLanguage: () => void;
  t: typeof translations['vi'];
}

const AppContext = createContext<AppContextType | undefined>(undefined);

export const AppProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [theme, setTheme] = useState<Theme>(() => {
    const saved = localStorage.getItem('sammicrawl_theme') as Theme;
    return saved === 'light' || saved === 'dark' ? saved : 'dark';
  });

  const [language, setLanguage] = useState<Language>(() => {
    const saved = localStorage.getItem('sammicrawl_lang') as Language;
    return saved === 'vi' || saved === 'en' ? saved : 'vi';
  });

  useEffect(() => {
    document.documentElement.setAttribute('data-theme', theme);
    localStorage.setItem('sammicrawl_theme', theme);
  }, [theme]);

  useEffect(() => {
    localStorage.setItem('sammicrawl_lang', language);
  }, [language]);

  const toggleTheme = () => {
    setTheme(prev => (prev === 'dark' ? 'light' : 'dark'));
  };

  const toggleLanguage = () => {
    setLanguage(prev => (prev === 'vi' ? 'en' : 'vi'));
  };

  const t = translations[language];

  return (
    <AppContext.Provider value={{ theme, language, toggleTheme, toggleLanguage, t }}>
      {children}
    </AppContext.Provider>
  );
};

export const useApp = () => {
  const ctx = useContext(AppContext);
  if (!ctx) {
    throw new Error('useApp must be used within an AppProvider');
  }
  return ctx;
};
