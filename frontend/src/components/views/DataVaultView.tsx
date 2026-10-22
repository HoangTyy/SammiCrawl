import React, { useState } from 'react';
import { 
  Database, 
  Search, 
  Filter, 
  FileSpreadsheet, 
  FileCode, 
  Server, 
  Sparkles
} from 'lucide-react';
import { useApp } from '../../context/AppContext';

interface DataVaultViewProps {
  onNavigateToStudio: () => void;
}

export const DataVaultView: React.FC<DataVaultViewProps> = ({ onNavigateToStudio }) => {
  const { t } = useApp();
  const [searchQuery, setSearchQuery] = useState('');
  const [filterType, setFilterType] = useState('all');

  return (
    <div className="view-container vault-view">
      {/* Top Header & Export Action */}
      <div className="view-header-strip">
        <div>
          <h2 className="view-title">{t.vault.title}</h2>
          <p className="view-subtitle">{t.vault.subtitle}</p>
        </div>

        {/* Export Toolbar */}
        <div className="vault-actions-row">
          <button type="button" className="btn-export-ghost" disabled>
            <FileCode size={14} />
            <span>{t.vault.btnExportJson}</span>
          </button>
          <button type="button" className="btn-export-ghost" disabled>
            <FileSpreadsheet size={14} />
            <span>{t.vault.btnExportExcel}</span>
          </button>
          <button type="button" className="btn-export-solid" disabled>
            <Server size={14} />
            <span>{t.vault.btnSyncEf}</span>
          </button>
        </div>
      </div>

      {/* Vault Telemetry Metrics */}
      <div className="vault-metrics-grid">
        <div className="vault-metric-tile">
          <span className="v-tile-label">{t.vault.tilePosts}</span>
          <span className="v-tile-val">0</span>
          <span className="v-tile-sub">{t.vault.tileNoRecord}</span>
        </div>
        <div className="vault-metric-tile">
          <span className="v-tile-label">{t.vault.tileComments}</span>
          <span className="v-tile-val">0</span>
          <span className="v-tile-sub">{t.vault.tileZeroAuthors}</span>
        </div>
        <div className="vault-metric-tile">
          <span className="v-tile-label">{t.vault.tileMedia}</span>
          <span className="v-tile-val">0</span>
          <span className="v-tile-sub">{t.vault.tileZeroMb}</span>
        </div>
        <div className="vault-metric-tile">
          <span className="v-tile-label">{t.vault.tileMetadata}</span>
          <span className="v-tile-val">0</span>
          <span className="v-tile-sub">{t.vault.tileAutoSync}</span>
        </div>
      </div>

      {/* Main Filter & Empty Vault Panel */}
      <div className="cyber-panel vault-panel">
        <div className="vault-filter-toolbar">
          <div className="search-box-cyber">
            <Search size={15} className="search-icon" />
            <input
              type="text"
              placeholder={t.vault.searchPlaceholder}
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              className="search-input-cyber"
            />
          </div>

          <div className="vault-filters-right">
            <select
              value={filterType}
              onChange={(e) => setFilterType(e.target.value)}
              className="vault-select"
            >
              <option value="all">{t.vault.filterAllSources}</option>
              <option value="page">{t.vault.filterPagesOnly}</option>
              <option value="group">{t.vault.filterGroupsOnly}</option>
              <option value="reel">{t.vault.filterReelsOnly}</option>
            </select>

            <button type="button" className="btn-filter-icon">
              <Filter size={15} />
              <span>{t.vault.btnAdvancedFilter}</span>
            </button>
          </div>
        </div>

        {/* Empty State */}
        <div className="cyber-empty-state">
          <div className="radar-empty-graphic">
            <div className="radar-circle ring-1"></div>
            <div className="radar-circle ring-2"></div>
            <Database size={32} className="radar-icon-center text-cyan" />
          </div>

          <h4 className="empty-heading">{t.vault.emptyHeading}</h4>
          <p className="empty-sub">{t.vault.emptySub}</p>

          <button 
            type="button" 
            className="btn-launch-cyber"
            onClick={onNavigateToStudio}
          >
            <Sparkles size={15} />
            <span>{t.vault.btnActivateFirst}</span>
          </button>
        </div>
      </div>
    </div>
  );
};
