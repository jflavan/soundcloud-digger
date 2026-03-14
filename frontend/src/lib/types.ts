export interface FeedTrack {
	title: string;
	artistName: string;
	artworkUrl: string | null;
	genre: string | null;
	tags: string[];
	likesCount: number;
	playbackCount: number;
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

export type SortBy = 'likes' | 'date';
export type TimeRange = '24h' | '7d' | '30d' | 'all';
