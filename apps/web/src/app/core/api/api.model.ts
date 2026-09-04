/** Mirrors the Stalksville API v1 contracts (observed/derived split included). */

export interface UserDto {
  id: string;
  username: string;
  role: string;
}

export interface LoginResponse {
  token: string;
  expiresAt: string;
  user: UserDto;
}

export interface AdminUserDto {
  id: string;
  username: string;
  role: string;
  createdAt: string;
  lastLoginAt: string | null;
}

export interface ApiKeyDto {
  id: string;
  name: string;
  prefix: string;
  createdAt: string;
  lastUsedAt: string | null;
  revokedAt: string | null;
}

export interface CreatedApiKeyDto {
  key: ApiKeyDto;
  apiKey: string;
}

export interface AuditEntryDto {
  id: string;
  userId: string | null;
  username: string | null;
  action: string;
  target: string | null;
  details: string | null;
  occurredAt: string;
}

export interface WatchedPlayerDto {
  id: string;
  wolvesvillePlayerId: string;
  username: string;
  starredAt: string;
}

export interface PlayerSummaryDto {
  id: string;
  wolvesvillePlayerId: string;
  username: string;
  firstSeenAt: string;
  lastSeenAt: string;
  currentClanId: string | null;
  currentClanName: string | null;
}

export interface ObservedStateDto {
  wolvesvillePlayerId: string;
  username: string;
  personalMessage: string | null;
  level: number | null;
  status: string | null;
  lastOnline: string | null;
  clanWolvesvilleId: string | null;
  wins: number;
  losses: number;
  gamesPlayed: number;
  receivedRosesCount: number | null;
  sentRosesCount: number | null;
  profileIconId: string | null;
  profileIconName: string | null;
  equippedAvatarId: string | null;
  badgeIds: string[];
  roleCardIds: string[];
  rankedSeason: number | null;
  rankedWins: number | null;
  rankedLosses: number | null;
  rankedCurrentRating: number | null;
  rankedPlacementRating: number | null;
  achievements: number | null;
  friendCount: number;
  capturedAt: string;
  lastObservedAt: string;
  observationCount: number;
  source: string;
  payloadHash: string;
}

export interface EvidenceDto {
  sourceType: string;
  sourceReference: string;
  capturedAt: string;
  payloadHash: string | null;
}

export interface ChangeDto {
  id: string;
  field: string;
  kind: string;
  oldValue: string | null;
  newValue: string | null;
  detectedAt: string;
  fromSnapshotId: string | null;
  toSnapshotId: string;
  evidence: EvidenceDto[];
}

export interface MembershipDto {
  clanId: string;
  wolvesvilleClanId: string;
  clanName: string | null;
  clanImported: boolean;
  startedAt: string;
  endedAt: string | null;
  isCurrent: boolean;
}

export interface RelationshipDto {
  type: string;
  targetEntityType: string;
  targetEntityId: string;
  targetName: string | null;
  confidence: number;
  firstObservedAt: string;
  lastObservedAt: string;
  isCurrent: boolean;
}

export interface FriendDto {
  playerId: string;
  username: string;
  current: boolean;
}

export interface DerivedDto {
  totalChanges: number;
  recentChanges: ChangeDto[];
  memberships: MembershipDto[];
  relationships: RelationshipDto[];
  friends: FriendDto[];
}

export interface PlayerDossierDto {
  player: PlayerSummaryDto;
  observed: ObservedStateDto | null;
  derived: DerivedDto;
}

export interface PlayerLookupResultDto {
  dossier: PlayerDossierDto;
  wasReobserved: boolean;
  changesDetectedInThisObservation: number;
}

export interface SnapshotDto {
  id: string;
  capturedAt: string;
  lastObservedAt: string;
  observationCount: number;
  payloadHash: string;
  source: string;
}

export interface ClanSummaryDto {
  id: string;
  wolvesvilleClanId: string;
  name: string | null;
  description: string | null;
  memberCount: number | null;
  languageCode: string | null;
  joinType: string | null;
  xps: number | null;
  level: number | null;
  firstSeenAt: string;
  lastSeenAt: string;
  lastImportedAt: string | null;
}

export interface ClanDossierDto {
  clan: ClanSummaryDto;
  knownMembers: PlayerSummaryDto[];
  openMembershipCount: number;
  membershipHistoryCount: number;
}

export interface ChangeWithPlayerDto {
  id: string;
  playerId: string;
  playerName: string;
  field: string;
  oldValue: string | null;
  newValue: string | null;
  detectedAt: string;
}

export interface DashboardStatsDto {
  playersTracked: number;
  clansTracked: number;
  snapshotsCollected: number;
  changesDetected: number;
  relationshipsDiscovered: number;
  recentChanges: ChangeWithPlayerDto[];
  recentlySeenPlayers: PlayerSummaryDto[];
}

export interface CapabilityDto {
  name: string;
  status: string;
  note?: string | null;
}

export interface ConnectionStatusDto {
  mode: string;
  status: string;
  message: string | null;
  writeOperationsEnabled: boolean;
  capabilities: CapabilityDto[];
}

export interface TimelineEventDto {
  id: string;
  entityType: string;
  entityId: string;
  eventType: string;
  summary: string | null;
  occurredAt: string;
  isDerived: boolean;
  confidence: number | null;
}

export interface AlertEvidenceChangeDto {
  id: string;
  field: string;
  oldValue: string | null;
  newValue: string | null;
  fromSnapshotId: string | null;
  toSnapshotId: string;
}

export interface AlertEvidenceDto {
  playerId: string;
  changes: AlertEvidenceChangeDto[];
}

export interface AlertDto {
  id: string;
  kind: string;
  severity: 'info' | 'notice' | 'warning';
  entityType: string;
  entityId: string;
  entityTitle: string;
  title: string;
  body: string;
  evidence: AlertEvidenceDto | null;
  createdAt: string;
  readAt: string | null;
}

export interface UnreadCountDto {
  unread: number;
}

export interface ProgressionPointDto {
  capturedAt: string;
  observationCount: number;
  level: number | null;
  wins: number;
  gamesPlayed: number;
  achievements: number | null;
}

export interface ProgressionDto {
  points: ProgressionPointDto[];
}

export interface ConnectorDto {
  nodeId: string;
  type: string;
  label: string;
  degree: number;
  betweenness: number;
}

export interface CommunityDto {
  index: number;
  size: number;
  memberIds: string[];
}

export interface GraphAnalyticsDto {
  nodeCount: number;
  edgeCount: number;
  topConnectors: ConnectorDto[];
  communities: CommunityDto[];
  note: string;
}

export interface HighscoreRowDto {
  rank: number;
  username: string;
  wolvesvillePlayerId: string;
  xp: number;
  playerId: string | null;
  tracked: boolean;
}

export interface HighscoreBoardDto {
  period: string;
  capturedAt: string | null;
  rows: HighscoreRowDto[];
}

export interface HighscoreCaptureResultDto {
  capturedAt: string;
  entriesStored: number;
  rankShiftAlerts: number;
}

export interface RankedRowDto {
  rank: number;
  username: string;
  wolvesvillePlayerId: string;
  skill: number;
  playerId: string | null;
  tracked: boolean;
}

export interface RankedBoardDto {
  seasonNumber: number | null;
  capturedAt: string | null;
  rows: RankedRowDto[];
}

export interface RankedSeasonDto {
  number: number;
  startTime: string;
  endTime: string;
  finished: boolean;
  startSkillDefault: number;
  source: string;
}

export interface RankedCaptureResultDto {
  capturedAt: string;
  seasonNumber: number;
  entriesStored: number;
  rankShiftAlerts: number;
}

export interface HallOfFameRowDto {
  position: number;
  playerName: string;
  wolvesvillePlayerId: string;
  avatarUrl: string | null;
  playerId: string | null;
  tracked: boolean;
}

export interface HallOfFameBoardDto {
  seasonNumber: number;
  capturedAt: string | null;
  rows: HallOfFameRowDto[];
  availableSeasons: number[];
}

export interface HallOfFameCaptureResultDto {
  capturedAt: string;
  seasonNumber: number;
  entriesStored: number;
  trackedWinnerAlerts: number;
}

export interface CatalogItemDto {
  kind: string;
  externalId: string;
  name: string;
  rarity: string;
  description: string | null;
  imageUrl: string | null;
}

export interface InvestigationSummaryDto {
  id: string;
  caseNumber: number;
  title: string;
  description: string | null;
  status: string;
  assignedToUserId: string | null;
  tags: string[];
  createdAt: string;
  updatedAt: string;
  targetCount: number;
  noteCount: number;
}

export interface SearchHitDto {
  type: string;
  id: string;
  title: string;
  subtitle: string | null;
}

export interface InvestigationTargetDto {
  id: string;
  entityType: string;
  entityId: string;
  displayName: string;
  addedAt: string;
  addedBy: string;
  snapshotCount: number;
  currentRelationships: number;
}

export interface InvestigationNoteDto {
  id: string;
  content: string;
  author: string;
  createdAt: string;
}

export interface InvestigationStatsDto {
  targets: number;
  timelineEvents: number;
  snapshotsCollected: number;
  highConfidenceRelationships: number;
}

export interface InvestigationWorkspaceDto {
  investigation: InvestigationSummaryDto;
  stats: InvestigationStatsDto;
  targets: InvestigationTargetDto[];
  notes: InvestigationNoteDto[];
  timeline: TimelineEventDto[];
}

// ---- Graph (phase 4) ----

export interface GraphNodeDto {
  id: string;
  type: 'player' | 'clan';
  label: string;
  snapshotCount: number;
  changeCount: number;
  imported: boolean;
}

export interface GraphEdgeDto {
  id: string;
  source: string;
  target: string;
  type: string;
  confidence: number;
  isCurrent: boolean;
  firstObservedAt: string;
  lastObservedAt: string;
}

export interface GraphDto {
  nodes: GraphNodeDto[];
  edges: GraphEdgeDto[];
}

export interface GraphPathNodeDto {
  id: string;
  type: string;
  label: string;
}

export interface GraphPathsDto {
  paths: GraphPathNodeDto[][];
}

// ---- Exposure & insights (phase 5) ----

export interface ExposureFactorDto {
  label: string;
  points: number;
  evidence: string;
}

export interface ExposureCategoryDto {
  name: string;
  score: number;
  factors: ExposureFactorDto[];
}

export interface ExposureResultDto {
  overall: number;
  categories: ExposureCategoryDto[];
}

export interface InsightDto {
  classification: string;
  title: string;
  description: string;
  confidence: number;
  evidenceChangeIds: string[];
}

// ---- Compare (phase 5) ----

export interface CompareFieldDto {
  field: string;
  a: string | null;
  b: string | null;
}

export interface CompareOverlapDto {
  kind: string;
  value: string;
  confidence: number;
  evidence: string;
}

export interface CompareResultDto {
  a: PlayerDossierDto;
  b: PlayerDossierDto;
  fields: CompareFieldDto[];
  overlaps: CompareOverlapDto[];
  disclaimer: string;
}

// ---- Analytics (phase 5) ----

export interface AnalyticsPointDto {
  date: string;
  value: number;
}

export interface AnalyticsSeriesDto {
  name: string;
  points: AnalyticsPointDto[];
}

export interface AnalyticsSummaryDto {
  playersTracked: number;
  clansTracked: number;
  snapshotsCollected: number;
  changesDetected: number;
  relationshipsDiscovered: number;
  activeInvestigations: number;
  series: AnalyticsSeriesDto[];
}

// ---- AI narrative (phase 6) ----

export interface AiNarrative {
  provider: string;
  model: string | null;
  generatedAt: string;
  observed: string[];
  derived: string[];
  hypothesis: string[];
  unknown: string[];
  disclaimer: string;
}

// ---- Scheduled scans & change notifications ----

export interface ScanScheduleDto {
  id: string;
  name: string;
  kind: string;
  intervalMinutes: number;
  batchSize: number;
  enabled: boolean;
  createdAt: string;
  updatedAt: string;
  lastRunAt: string | null;
  nextRunAt: string | null;
}

export interface ScanFieldChangeDto {
  field: string;
  oldValue: string | null;
  newValue: string | null;
}

export interface ScanChangeLineDto {
  player: string;
  changes: number;
  fields: ScanFieldChangeDto[];
}

export interface ScanRunDto {
  id: string;
  scheduleId: string;
  scheduleName: string;
  status: string;
  startedAt: string;
  finishedAt: string;
  playersObserved: number;
  changesDetected: number;
  alertsRaised: number;
  error: string | null;
  changes: ScanChangeLineDto[];
}

export interface NotificationChannelDto {
  id: string;
  name: string;
  kind: string;
  targetUrlMasked: string;
  enabled: boolean;
  createdAt: string;
  lastDeliveryAt: string | null;
  lastDeliveryStatus: string | null;
}
