export const defaults = {
  brand: 'Geo Sniper', releaseLive: false, packageName: 'com.geosniper.game',
  timeZone: 'Asia/Kolkata', delivery: 'export', publishingEnabled: false,
  youtubePrivacy: 'private', youtubeProfile: '', instagramProfile: ''
};

// Local editorial templates, not an AI service. Claims intentionally stay within README capabilities.
export const concepts = [
  ['gps', 'Your city. Your next mission.', 'What if your city became a sniper map?', 'Map to mission', 'Show a named public district on the map, then the same streets in gameplay. Use a wide location, not a home address.', 'Geo Sniper turns real-world map data into fictional sniper missions. Use optional GPS nearby, or search for a place. Buildings are game interpretations, not exact replicas.'],
  ['skill', 'Would you take this shot?', 'Wait for the opening.', 'One clean shot', 'Record one encounter: target behind cover, a visible opening, scope, shot and reaction. No instant replay presented as a second kill.', 'A small opening. One careful shot. Pick your moment in Geo Sniper.'],
  ['gps', 'Recognize these streets?', 'From a map to a mission.', 'Recognizable streets', 'Show a public road label and its mapped layout. Name the area only if the recording proves it. Blur precise coordinates.', 'Familiar roads. A different perspective. Explore game sectors shaped by real-world streets and building footprints.'],
  ['devlog', 'A better scope, one detail at a time.', 'Which scope feels better?', 'Scope feel', 'Record an honest before/after of two builds. Label both versions clearly. Show drag, zoom and recoil without performance claims.', 'Building Geo Sniper: tuning the scope so each adjustment feels deliberate. What would you change?'],
  ['gps', 'Pick a place. Find a new angle.', 'Your next map is a real place.', 'Place search', 'Record a successful place search and the loaded district. Keep loading time honest; label time cuts.', 'No GPS permission? You can search for a place by name. Map availability and detail depend on source data and connectivity.'],
  ['skill', 'Move. Settle. Take the shot.', 'Patience wins this encounter.', 'Aim discipline', 'Show movement, settling the scope and a well-timed shot in one current-build encounter.', 'A little patience changes the shot. A precision moment from Geo Sniper.'],
  ['gps', 'Which city should we explore next?', 'Your city could inspire the next clip.', 'Community choice', 'Invite broad city suggestions. Next episode should show a genuine recorded search, not a fabricated local landmark.', 'Tell us a city you would like to see explored. City names only—please do not share home addresses or precise locations.'],
  ['devlog', 'Making a mapped city playable.', 'Real map data. Fictional missions.', 'Behind the map', 'Show map geometry and a grounded gameplay route. Explain one limitation: generic facades or missing source detail.', 'Geo Sniper uses real-world map data with game-built environments and fictional encounters. This is not live satellite imagery.'],
  ['skill', 'The rival moved. Now what?', 'Find a new angle.', 'AI duel', 'Capture a fair AI duel with cover changes and clear incoming-hit feedback. Label the opponent AI.', 'A duel against an AI rival. Read the cover, adjust your position and look for an opening.'],
  ['gps', 'Same game. A different neighborhood.', 'Two places. Two different layouts.', 'Sector contrast', 'Cut between two real recorded public districts. Label areas only when verified; do not show private coordinates.', 'Different mapped streets create different game layouts. Where would you explore first?'],
  ['devlog', 'Your feedback shapes the next build.', 'One change that made aiming better.', 'Build diary', 'Show one shipped improvement in the current build. Ask one focused question; avoid promising a release date.', 'A small Geo Sniper development update. Tell us which detail matters most to you.'],
  ['gps', 'The map is only the beginning.', 'Discover a different side of your city.', 'GPS recap', 'Cut map, mapped streets and scope into a 15-second sequence. Keep GPS permission optional in captions.', 'Choose a place. Explore the mapped sector. Take on a fictional sniper mission in Geo Sniper.']
].map(([pillar, hookA, hookB, name, brief, description], index) => ({index, pillar, hookA, hookB, name, brief, description}));

export function caption(concept, channel, settings) {
  const cta = settings.releaseLive
    ? `Geo Sniper is on Google Play. Find the store link in our ${channel === 'youtube' ? 'channel profile' : 'bio'}.`
    : 'Coming to Android. Google Play review is pending. Follow for the launch update.';
  return `${concept.description}\n\n${cta}\n\n#GeoSniper #AndroidGaming ${concept.pillar === 'gps' ? '#LocationBasedGame' : '#SniperGame'}`;
}

export function makePlan(start, settings) {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(start) || !Number.isFinite(Date.parse(start))) throw Error('Use a valid YYYY-MM-DD start date.');
  if (new Date(start).toISOString().slice(0,10) !== start) throw Error('Invalid calendar date.');
  const posts = [];
  // Twelve original concepts in four weeks; cross-post each, not 24 near-duplicate clips.
  for (let week = 0; week < 4; week++) for (let slot = 0; slot < 3; slot++) {
    const concept = concepts[week * 3 + slot];
    const date = new Date(`${start}T00:00:00.000Z`); date.setUTCDate(date.getUTCDate() + week * 7 + slot * 2);
    const day = date.toISOString().slice(0,10), variant = week % 2 ? 'B' : 'A';
    for (const channel of ['youtube', 'instagram']) posts.push({
      id: `${day}-${channel}`, channel, pillar: concept.pillar, concept: concept.name, variant,
      title: variant === 'A' ? concept.hookA : concept.hookB,
      caption: caption(concept, channel, settings), brief: concept.brief,
      scheduledAt: new Date(`${day}T${channel === 'youtube' ? '19:00' : '19:30'}:00+05:30`).toISOString(),
      status: 'draft', assetId: '', revision: 1, approvalHash: '', publishedUrl: '', error: '', remoteId: '',
      releaseLive: settings.releaseLive, createdAt: new Date().toISOString()
    });
  }
  return posts;
}

export function campaignLink(channel, settings) {
  if (!settings.releaseLive) return '';
  const tags = new URLSearchParams({utm_source: channel, utm_medium: 'organic_social', utm_campaign: 'geosniper_launch'});
  const url = new URL('https://play.google.com/store/apps/details');
  url.searchParams.set('id', settings.packageName);
  url.searchParams.set('referrer', tags.toString());
  return url.toString();
}
