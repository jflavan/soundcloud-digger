export interface FeedTrack {
	title: string;
	artistName: string;
	artworkUrl: string | null;
	genre: string | null;
	tags: string[];
	likesCount: number;
	playbackCount: number;
	repostsCount: number;
	commentCount: number;
	createdAt: string;
	permalinkUrl: string | null;
	duration: number;
	access: string | null;
	activityType: string;
	appearedAt: string;
}

export interface FeedResponse {
	tracks: FeedTrack[];
	totalCount: number;
	loadingComplete: boolean;
}

export type SortBy = 'likes' | 'date' | 'plays' | 'reposts' | 'comments';
export type DiscoverSortBy = SortBy | 'reposterCount';
export type TimeRange = '24h' | '7d' | '30d' | 'all';
export type TimeField = 'feed' | 'uploaded';

export interface DiscoverTrack extends FeedTrack {
	reposterCount: number;
	reposters: string[];
	lastRepostedAt: string;
}

export interface DiscoverResponse {
	tracks: DiscoverTrack[];
	totalCount: number;
	loadingComplete: boolean;
	lastRefreshedAt: string | null;
	progress: number;
}
