// Comprehensive Global City Database and Dynamic Location Resolver

export const WORLD_CITIES = [
  {
    name: 'Tokyo', country: 'Japan', lat: 35.6762, lng: 139.6503,
    district: 'SHIBUYA CROSSING', mission: 'SHIBUYA SKYLINE RECON',
    target: 'Syndicate Courier · Penthouse Terrace', rangeM: 148, wind: '2.4 M/S [←]', elev: '+34 M',
    nearby: [
      { name: 'Shinjuku Skyscraper Hub', dist: '1.8 KM', dir: 'NW', dx: -280, dz: -190 },
      { name: 'Roppongi Hills Spire', dist: '2.4 KM', dir: 'SE', dx: 240, dz: 280 },
      { name: 'Akihabara Tech Corridor', dist: '3.1 KM', dir: 'NE', dx: 290, dz: -220 }
    ]
  },
  {
    name: 'New York', country: 'United States', lat: 40.7128, lng: -74.0060,
    district: 'MANHATTAN FINANCIAL', mission: 'HIGH TOWER OVERWATCH',
    target: 'Arms Broker · Helipad Extraction', rangeM: 285, wind: '4.1 M/S [→]', elev: '+82 M',
    nearby: [
      { name: 'Empire Spire Overwatch', dist: '1.2 KM', dir: 'N', dx: -110, dz: -310 },
      { name: 'Brooklyn Bridge Sector', dist: '2.1 KM', dir: 'SE', dx: 250, dz: 270 },
      { name: 'Hudson Yards Highline', dist: '1.9 KM', dir: 'W', dx: -330, dz: 70 }
    ]
  },
  {
    name: 'London', country: 'United Kingdom', lat: 51.5074, lng: -0.1278,
    district: 'CANARY WHARF', mission: 'RIVER FRONT VANTAGE',
    target: 'Black Market VIP · Cargo Docks', rangeM: 210, wind: '1.8 M/S [←]', elev: '+45 M',
    nearby: [
      { name: 'The Shard Vantage', dist: '1.5 KM', dir: 'SW', dx: -230, dz: 210 },
      { name: 'Tower Bridge Perimeter', dist: '1.1 KM', dir: 'S', dx: 50, dz: 280 },
      { name: 'Bank Financial Axis', dist: '0.9 KM', dir: 'N', dx: 40, dz: -260 }
    ]
  },
  {
    name: 'Paris', country: 'France', lat: 48.8566, lng: 2.3522,
    district: 'LA DÉFENSE ARCH', mission: 'GLASS ARCH SURVEILLANCE',
    target: 'Covert Envoy · Luxury Suite', rangeM: 175, wind: '2.0 M/S [←]', elev: '+38 M',
    nearby: [
      { name: 'Eiffel Perimeter Watch', dist: '2.8 KM', dir: 'SW', dx: -310, dz: 190 },
      { name: 'Champs-Élysées Axis', dist: '1.6 KM', dir: 'NW', dx: -200, dz: -250 },
      { name: 'Montparnasse Tower', dist: '2.2 KM', dir: 'S', dx: 70, dz: 330 }
    ]
  },
  {
    name: 'Dubai', country: 'United Arab Emirates', lat: 25.2048, lng: 55.2708,
    district: 'DOWNTOWN MARINA', mission: 'BURJ OVERWATCH PERCH',
    target: 'Rogue Executive · Sky Lounge', rangeM: 340, wind: '3.5 M/S [→]', elev: '+110 M',
    nearby: [
      { name: 'Burj Khalifa Apex', dist: '0.8 KM', dir: 'N', dx: -50, dz: -230 },
      { name: 'Palm Jumeirah Perimeter', dist: '3.5 KM', dir: 'SW', dx: -360, dz: 240 },
      { name: 'DIFC Financial Gate', dist: '1.4 KM', dir: 'NE', dx: 240, dz: -170 }
    ]
  },
  {
    name: 'Mumbai', country: 'India', lat: 19.0760, lng: 72.8777,
    district: 'BANDRA-KURLA COMPLEX', mission: 'BKC PERIMETER OVERWATCH',
    target: 'Smuggling Operative · Helipad Pad 3', rangeM: 195, wind: '2.9 M/S [←]', elev: '+29 M',
    nearby: [
      { name: 'Bandra Sealink Watch', dist: '2.1 KM', dir: 'W', dx: -300, dz: 50 },
      { name: 'Kurla Transit Sector', dist: '1.3 KM', dir: 'E', dx: 270, dz: -80 },
      { name: 'Dharavi Perimeter Perch', dist: '1.7 KM', dir: 'S', dx: -70, dz: 270 }
    ]
  },
  {
    name: 'Seoul', country: 'South Korea', lat: 37.5665, lng: 126.9780,
    district: 'GANGNAM CYBER DISTRICT', mission: 'NEON SPIDER RECON',
    target: 'Data Broker · High-Rise Balcony', rangeM: 230, wind: '3.1 M/S [→]', elev: '+52 M',
    nearby: [
      { name: 'Lotte World Tower Spire', dist: '2.6 KM', dir: 'SE', dx: 260, dz: 290 },
      { name: 'Teheran-ro Tech Axis', dist: '0.9 KM', dir: 'W', dx: -240, dz: 30 },
      { name: 'Han River Bridge Watch', dist: '1.9 KM', dir: 'N', dx: -30, dz: -280 }
    ]
  },
  {
    name: 'Berlin', country: 'Germany', lat: 52.5200, lng: 13.4050,
    district: 'MITTE // ALEXANDERPLATZ', mission: 'TELECOM TOWER OVERWATCH',
    target: 'Syndicate Cell · Rooftop Antenna', rangeM: 265, wind: '2.1 M/S [←]', elev: '+55 M',
    nearby: [
      { name: 'TV Tower Apex Vantage', dist: '0.4 KM', dir: 'N', dx: 20, dz: -220 },
      { name: 'Potsdamer Platz Hub', dist: '2.1 KM', dir: 'SW', dx: -290, dz: 180 },
      { name: 'Museum Island Perimeter', dist: '1.2 KM', dir: 'NW', dx: -180, dz: -190 }
    ]
  },
  {
    name: 'Sydney', country: 'Australia', lat: -33.8688, lng: 151.2093,
    district: 'CIRCULAR QUAY', mission: 'HARBOR SPIRE WATCH',
    target: 'Cartel Escort · Penthouse Deck', rangeM: 310, wind: '3.8 M/S [→]', elev: '+64 M',
    nearby: [
      { name: 'Harbour Bridge Vantage', dist: '1.4 KM', dir: 'NW', dx: -220, dz: -230 },
      { name: 'Opera House Forecourt', dist: '1.1 KM', dir: 'NE', dx: 210, dz: -160 },
      { name: 'Barangaroo Towers', dist: '1.6 KM', dir: 'W', dx: -300, dz: 60 }
    ]
  },
  {
    name: 'Rome', country: 'Italy', lat: 41.9028, lng: 12.4964,
    district: 'EUR BUSINESS DISTRICT', mission: 'HISTORIC AXIS OVERWATCH',
    target: 'Courier Cell · Hotel Balcony', rangeM: 185, wind: '1.9 M/S [←]', elev: '+32 M',
    nearby: [
      { name: 'Colosseum Outer Rim', dist: '2.4 KM', dir: 'NE', dx: 230, dz: -200 },
      { name: 'Tiber River Quayside', dist: '1.5 KM', dir: 'W', dx: -270, dz: 80 },
      { name: 'Vatican Perimeter Post', dist: '3.2 KM', dir: 'NW', dx: -320, dz: -170 }
    ]
  },
  {
    name: 'Singapore', country: 'Singapore', lat: 1.3521, lng: 103.8198,
    district: 'MARINA BAY FINANCIAL', mission: 'BAYFRONT PERIMETER RECON',
    target: 'Financier VIP · SkyPark Pool', rangeM: 225, wind: '2.3 M/S [→]', elev: '+78 M',
    nearby: [
      { name: 'Marina Bay Sands Perch', dist: '0.7 KM', dir: 'E', dx: 220, dz: -30 },
      { name: 'Gardens Spire Watch', dist: '1.3 KM', dir: 'SE', dx: 180, dz: 250 },
      { name: 'Raffles Place Canyon', dist: '0.9 KM', dir: 'W', dx: -210, dz: 60 }
    ]
  },
  {
    name: 'Hong Kong', country: 'China', lat: 22.3193, lng: 114.1694,
    district: 'CENTRAL // VICTORIA HARBOR', mission: 'SKYLINE NEST OVERWATCH',
    target: 'Triad Boss · Private Helipad', rangeM: 290, wind: '3.4 M/S [←]', elev: '+95 M',
    nearby: [
      { name: 'Victoria Peak Overlook', dist: '1.9 KM', dir: 'SW', dx: -260, dz: 220 },
      { name: 'ICC Tower West Kowloon', dist: '2.4 KM', dir: 'NW', dx: -280, dz: -200 },
      { name: 'Wan Chai Heliport', dist: '1.5 KM', dir: 'E', dx: 270, dz: 40 }
    ]
  },
  {
    name: 'Los Angeles', country: 'United States', lat: 34.0522, lng: -118.2437,
    district: 'DOWNTOWN LA HIGHRISE', mission: 'WILSHIRE TOWER VANTAGE',
    target: 'Heist Mastermind · Rooftop Terrace', rangeM: 240, wind: '2.6 M/S [→]', elev: '+68 M',
    nearby: [
      { name: 'US Bank Tower Apex', dist: '0.5 KM', dir: 'N', dx: -40, dz: -210 },
      { name: 'Arts District Perimeter', dist: '1.8 KM', dir: 'E', dx: 280, dz: 30 },
      { name: 'Convention Center Watch', dist: '1.4 KM', dir: 'S', dx: 60, dz: 280 }
    ]
  },
  {
    name: 'San Francisco', country: 'United States', lat: 37.7749, lng: -122.4194,
    district: 'TRANSAMERICA CORRIDOR', mission: 'EMBARCADERO OVERWATCH',
    target: 'Tech Syndicate · Glass Atrium', rangeM: 275, wind: '4.5 M/S [←]', elev: '+72 M',
    nearby: [
      { name: 'Salesforce Tower Spire', dist: '0.8 KM', dir: 'SE', dx: 190, dz: 220 },
      { name: 'Ferry Building Docks', dist: '1.2 KM', dir: 'NE', dx: 220, dz: -180 },
      { name: 'Nob Hill High Perch', dist: '1.5 KM', dir: 'W', dx: -270, dz: -40 }
    ]
  },
  {
    name: 'Chicago', country: 'United States', lat: 41.8781, lng: -87.6298,
    district: 'THE LOOP // WACKER DR', mission: 'RIVER TOWER VANTAGE',
    target: 'Mob Syndicate · 42nd Floor Balcony', rangeM: 315, wind: '4.8 M/S [→]', elev: '+85 M',
    nearby: [
      { name: 'Willis Tower Skydeck', dist: '0.9 KM', dir: 'W', dx: -250, dz: 40 },
      { name: 'Navy Pier Approach', dist: '2.1 KM', dir: 'NE', dx: 260, dz: -210 },
      { name: 'Magnificent Mile Axis', dist: '1.4 KM', dir: 'N', dx: -20, dz: -290 }
    ]
  },
  {
    name: 'Toronto', country: 'Canada', lat: 43.6532, lng: -79.3832,
    district: 'BAY STREET FINANCIAL', mission: 'CN TOWER PROXIMITY WATCH',
    target: 'Arms Cartel Envoy · Rooftop Helipad', rangeM: 260, wind: '3.2 M/S [←]', elev: '+62 M',
    nearby: [
      { name: 'CN Tower Crow Nest', dist: '1.1 KM', dir: 'SW', dx: -210, dz: 200 },
      { name: 'Union Station Sector', dist: '0.7 KM', dir: 'S', dx: 30, dz: 220 },
      { name: 'Eaton Centre Roof', dist: '0.8 KM', dir: 'N', dx: -40, dz: -240 }
    ]
  },
  {
    name: 'São Paulo', country: 'Brazil', lat: -23.5505, lng: -46.6333,
    district: 'AVENIDA PAULISTA', mission: 'PAULISTA SPIRE RECON',
    target: 'Cartel Commander · Helipad', rangeM: 280, wind: '2.7 M/S [→]', elev: '+58 M',
    nearby: [
      { name: 'MASP Terrace Sector', dist: '0.6 KM', dir: 'NW', dx: -180, dz: -140 },
      { name: 'Consolação Perimeter', dist: '1.5 KM', dir: 'W', dx: -280, dz: 50 },
      { name: 'Jardins Skyline Post', dist: '1.8 KM', dir: 'S', dx: 50, dz: 290 }
    ]
  },
  {
    name: 'Cairo', country: 'Egypt', lat: 30.0444, lng: 31.2357,
    district: 'ZAMALEK // NILE FRONT', mission: 'NILE TOWER OVERWATCH',
    target: 'Black Market Broker · River Balcony', rangeM: 205, wind: '3.0 M/S [←]', elev: '+40 M',
    nearby: [
      { name: 'Cairo Tower Spire', dist: '0.7 KM', dir: 'S', dx: 20, dz: 230 },
      { name: 'Tahrir Square Axis', dist: '1.6 KM', dir: 'E', dx: 270, dz: -20 },
      { name: 'Gezira Island North', dist: '1.2 KM', dir: 'N', dx: -30, dz: -260 }
    ]
  },
  {
    name: 'Cape Town', country: 'South Africa', lat: -33.9249, lng: 18.4241,
    district: 'FORESHORE WATERFRONT', mission: 'HARBOR RIDGE VANTAGE',
    target: 'Smuggler Transport · Dock Quayside', rangeM: 325, wind: '4.2 M/S [→]', elev: '+52 M',
    nearby: [
      { name: 'Table Mountain Lower Ridge', dist: '3.1 KM', dir: 'S', dx: 60, dz: 320 },
      { name: 'V&A Waterfront Basin', dist: '1.4 KM', dir: 'NW', dx: -240, dz: -190 },
      { name: 'Signal Hill Overlook', dist: '2.2 KM', dir: 'W', dx: -310, dz: 80 }
    ]
  },
  {
    name: 'Bangkok', country: 'Thailand', lat: 13.7563, lng: 100.5018,
    district: 'SILOM // SATHORN AXIS', mission: 'SKY BAR OVERWATCH',
    target: 'Shadow Broker · 58th Floor Lounge', rangeM: 245, wind: '2.2 M/S [←]', elev: '+75 M',
    nearby: [
      { name: 'King Power Mahanakhon', dist: '0.8 KM', dir: 'SE', dx: 190, dz: 210 },
      { name: 'Chao Phraya Pier Watch', dist: '1.7 KM', dir: 'W', dx: -290, dz: 40 },
      { name: 'Lumphini Park Perimeter', dist: '1.3 KM', dir: 'NE', dx: 230, dz: -180 }
    ]
  }
];

export const CITIES = WORLD_CITIES.map(c => ({
  id: c.name.toLowerCase().replace(/[^a-z0-9]/g, ''),
  coords: `${Math.abs(c.lat).toFixed(4)}° ${c.lat >= 0 ? 'N' : 'S'}, ${Math.abs(c.lng).toFixed(4)}° ${c.lng >= 0 ? 'E' : 'W'}`,
  ...c
}));

export function formatCoords(lat, lng) {
  return `${Math.abs(lat).toFixed(4)}° ${lat >= 0 ? 'N' : 'S'}, ${Math.abs(lng).toFixed(4)}° ${lng >= 0 ? 'E' : 'W'}`;
}

// Clean /location, ;/location, or manual prefixes from input
export function cleanLocationQuery(input) {
  if (!input || typeof input !== 'string') return '';
  return input
    .trim()
    .replace(/^[;:,/#!$%^&*]+\s*/, '')   // Remove leading ;, /, :, etc
    .replace(/^location\s+/i, '')       // Remove "location "
    .replace(/^\/?location[:=\s]*/i, '') // Remove "/location "
    .trim();
}

// Deterministic string hash for procedural generation
function hashStr(str) {
  let hash = 0;
  for (let i = 0; i < str.length; i++) {
    hash = ((hash << 5) - hash) + str.charCodeAt(i);
    hash |= 0;
  }
  return Math.abs(hash);
}

// Cache for resolved locations
const locationCache = new Map();

const BAD_LANDMARK_WORDS = [
  'championship', 'tournament', 'cup', 'olympics', 'games', 'league', 'prix', 'derby',
  'battle', 'siege', 'war', 'riot', 'massacre', 'bombing', 'conflict', 'rebellion',
  'shooting', 'assassination', 'murder', 'crime', 'scandal', 'controversy', 'strike', 'protest',
  'election', 'constituency', 'referendum', 'census', 'assembly', 'vidhan', 'lok sabha', 'parliamentary', 'district', 'tehsil', 'mandal',
  'famine', 'plague', 'earthquake', 'flood', 'disaster', 'crash', 'accident', 'fire', 'epidemic',
  'treaty', 'accord', 'protocol', 'declaration', 'dynasty', 'empire', 'kingdom',
  'timeline', 'list of', 'history of', 'demographics', 'economy of', 'politics of', 'geography of', 'climate of'
];

function cleanLandmarkName(title) {
  return title
    .replace(/,\s*.*$/, '')
    .replace(/\(.*?\)/g, '')
    .replace(/\s+/g, ' ')
    .trim();
}

export async function resolveLandmarks(lat, lon, cityName) {
  let rawList = [];

  // 1. Wikipedia Geosearch (radius up to 10km)
  if (lat !== null && lon !== null) {
    try {
      const wikiUrl = `https://en.wikipedia.org/w/api.php?action=query&list=geosearch&gscoord=${lat}%7C${lon}&gsradius=10000&gslimit=40&format=json&origin=*`;
      const res = await fetch(wikiUrl, {
        headers: { 'User-Agent': 'GeoSniperGame/1.0 (contact@geosniper.app)' }
      }).then(r => r.json());
      rawList = (res.query?.geosearch || []).map(p => ({
        title: p.title,
        dist: p.dist,
        lat: p.lat,
        lon: p.lon
      }));
    } catch (e) {
      console.warn('Geosearch error:', e?.message);
    }
  }

  // Filter out historical battles, championships, political bodies, elections
  let filtered = rawList.filter(item => {
    const t = item.title.toLowerCase();
    const c = cityName.toLowerCase();
    if (t === c) return false;
    if (item.dist < 150 && (t.includes(c) || c.includes(t))) return false;
    if (BAD_LANDMARK_WORDS.some(w => t.includes(w))) return false;
    return true;
  });

  // 2. If fewer than 3, fallback to Wikipedia Search for real physical facilities & attractions
  if (filtered.length < 3) {
    try {
      const searchUrl = `https://en.wikipedia.org/w/api.php?action=query&list=search&srsearch=${encodeURIComponent(cityName + ' railway station OR stadium OR airport OR monument OR temple OR fort OR park OR museum')}&format=json&origin=*`;
      const sRes = await fetch(searchUrl, {
        headers: { 'User-Agent': 'GeoSniperGame/1.0 (contact@geosniper.app)' }
      }).then(r => r.json());
      const sList = sRes.query?.search || [];
      for (const s of sList) {
        const t = s.title.toLowerCase();
        if (t === cityName.toLowerCase()) continue;
        if (BAD_LANDMARK_WORDS.some(w => t.includes(w))) continue;
        if (!filtered.some(f => f.title.toLowerCase() === t)) {
          filtered.push({
            title: s.title,
            dist: 1200 + (filtered.length * 1500),
            lat: lat ? lat + ((filtered.length + 1) * 0.015) : 0,
            lon: lon ? lon + ((filtered.length + 1) * 0.012) : 0
          });
        }
        if (filtered.length >= 6) break;
      }
    } catch (e) {
      console.warn('Search landmark fallback error:', e?.message);
    }
  }

  // Pick 3 diverse landmarks across distances
  const close = filtered.find(p => p.dist < 2000) || filtered[0];
  const mid = filtered.find(p => p !== close && p.dist >= 1500 && p.dist < 5000) || filtered.find(p => p !== close) || filtered[1];
  const far = filtered.slice().reverse().find(p => p !== close && p !== mid) || filtered.find(p => p !== close && p !== mid) || filtered[2];
  const candidates = [close, mid, far].filter(Boolean);

  const dirs = ['N', 'NE', 'E', 'SE', 'S', 'SW', 'W', 'NW'];
  return candidates.map((p, idx) => {
    let dir = 'NE';
    let rad = 0;
    if (lat && lon && p.lat && p.lon) {
      const dLat = p.lat - lat, dLon = p.lon - lon;
      const angle = Math.atan2(dLon, dLat) * 180 / Math.PI;
      dir = dirs[Math.round(((angle + 360) % 360) / 45) % 8];
      rad = angle * Math.PI / 180;
    } else {
      const defaultAngles = [45, 135, 270];
      const a = defaultAngles[idx % defaultAngles.length];
      rad = a * Math.PI / 180;
      dir = dirs[Math.round(a / 45) % 8];
    }
    const r = Math.min(320, 160 + (idx * 60));
    return {
      name: cleanLandmarkName(p.title),
      dist: (p.dist / 1000).toFixed(1) + ' KM',
      dir,
      dx: Math.round(Math.sin(rad) * r),
      dz: Math.round(-Math.cos(rad) * r)
    };
  });
}

export async function fetchRealLocation(input) {
  if (typeof input === 'object' && input !== null && input.name) return input;
  const cleaned = cleanLocationQuery(input);
  if (!cleaned) return getCityForDate();

  const q = cleaned.toLowerCase();
  // Only return from cache if it has real verified landmarks
  if (locationCache.has(q)) {
    const cached = locationCache.get(q);
    if (cached && cached.nearby && cached.nearby.length && !cached.isFallback) {
      return cached;
    }
  }

  // 1. Direct match in preset database
  const directMatch = CITIES.find(c =>
    c.name.toLowerCase() === q ||
    c.id === q ||
    c.country.toLowerCase() === q ||
    q.includes(c.name.toLowerCase()) ||
    c.name.toLowerCase().includes(q)
  );
  if (directMatch) {
    locationCache.set(q, directMatch);
    return directMatch;
  }

  // 2. In browser environment, query local backend API endpoint (bypasses browser CORS & User-Agent restrictions)
  if (typeof window !== 'undefined' && window.location) {
    try {
      const apiUrl = `/api/location?q=${encodeURIComponent(cleaned)}`;
      const res = await fetch(apiUrl, { cache: 'no-store' }).then(r => r.json());
      if (res && res.name && res.nearby && res.nearby.length && !res.isFallback) {
        locationCache.set(q, res);
        locationCache.set(res.name.toLowerCase(), res);
        return res;
      }
    } catch (e) {
      console.warn('Backend /api/location unavailable, attempting direct fetch:', e?.message);
    }
  }

  // 3. Check for manual lat, lng input e.g. "29.2145, 79.5279"
  const coordMatch = cleaned.match(/^(-?\d+(\.\d+)?)[,\s]+(-?\d+(\.\d+)?)$/);
  let lat = null, lon = null, cityName = cleaned, stateOrRegion = '', country = 'GLOBAL OPS';

  if (coordMatch) {
    lat = parseFloat(coordMatch[1]);
    lon = parseFloat(coordMatch[3]);
    cityName = `GPS ${lat.toFixed(2)}, ${lon.toFixed(2)}`;
  } else {
    // 4. Real Geocoding with OpenStreetMap Nominatim
    try {
      const geoUrl = `https://nominatim.openstreetmap.org/search?q=${encodeURIComponent(cleaned)}&format=json&limit=1&addressdetails=1`;
      const geoRes = await fetch(geoUrl, {
        headers: { 'User-Agent': 'GeoSniperGame/1.0 (contact@geosniper.app)' }
      }).then(r => r.json());

      if (geoRes && geoRes.length > 0) {
        const loc = geoRes[0];
        lat = parseFloat(loc.lat);
        lon = parseFloat(loc.lon);
        const addr = loc.address || {};
        cityName = addr.city || addr.town || addr.village || addr.municipality || addr.suburb || loc.name;
        stateOrRegion = addr.state || addr.county || '';
        country = addr.country || 'GLOBAL OPS';
      }
    } catch (e) {
      console.warn('Geocoding fallback:', e?.message);
    }
  }

  // 5. Fetch REAL Nearby Places & Landmarks
  let nearby = [];
  if (lat !== null && lon !== null) {
    nearby = await resolveLandmarks(lat, lon, cityName);
  }

  const isFallback = !nearby.length;
  if (isFallback) {
    // If no network landmarks found, use clean directional sectors without generic placeholder text
    nearby = [
      { name: `${cityName} Station Terminal`, dist: '1.2 KM', dir: 'N', dx: 0, dz: -220 },
      { name: `${cityName} Central Plaza`, dist: '1.9 KM', dir: 'SW', dx: -240, dz: 180 },
      { name: `${cityName} Highway Junction`, dist: '2.6 KM', dir: 'SE', dx: 230, dz: 240 }
    ];
  }

  const finalLat = lat ?? 28.6139;
  const finalLon = lon ?? 77.2090;
  const h = hashStr(cleaned);
  const rangeM = 160 + (h % 180);
  const windSpd = (1.5 + ((h % 30) / 10)).toFixed(1);
  const windDir = (h % 2 === 0) ? '[←]' : '[→]';
  const elevM = 25 + (h % 60);

  const cleanCountry = stateOrRegion ? `${stateOrRegion}, ${country}` : country;
  const result = {
    id: cityName.toLowerCase().replace(/[^a-z0-9]/g, '_'),
    name: cityName,
    country: cleanCountry,
    coords: formatCoords(finalLat, finalLon),
    lat: finalLat,
    lng: finalLon,
    district: `${cityName.toUpperCase()} // ${stateOrRegion.toUpperCase() || 'TACTICAL SECTOR'}`,
    mission: `OPERATION ${cityName.toUpperCase()} OVERWATCH`,
    target: nearby[0] ? `Hostile Cell · Near ${nearby[0].name}` : `Target Spire · ${cityName}`,
    rangeM,
    wind: `${windSpd} M/S ${windDir}`,
    elev: `+${elevM} M`,
    nearby,
    isFallback
  };

  // Only cache if real landmarks were successfully retrieved
  if (!isFallback) {
    locationCache.set(q, result);
    locationCache.set(cityName.toLowerCase(), result);
  }
  return result;
}

export function resolveLocation(input) {
  if (typeof input === 'object' && input !== null && input.name) return input;
  const cleaned = cleanLocationQuery(input);
  if (!cleaned) return getCityForDate();
  const q = cleaned.toLowerCase();

  // If in cache, return immediately
  if (locationCache.has(q)) return locationCache.get(q);

  // Direct match in preset database
  const directMatch = CITIES.find(c =>
    c.name.toLowerCase() === q ||
    c.id === q ||
    c.country.toLowerCase() === q ||
    q.includes(c.name.toLowerCase()) ||
    c.name.toLowerCase().includes(q)
  );
  if (directMatch) return directMatch;

  // Kick off background real fetch to populate cache
  fetchRealLocation(cleaned).catch(() => {});

  // Capitalize cleanly for synchronous fallback
  const cleanName = cleaned.split(',')[0].trim().replace(/\b\w/g, l => l.toUpperCase());
  const h = hashStr(cleaned);
  const pseudoLat = ((h % 14000) / 100) - 60;
  const pseudoLng = (((h >> 3) % 36000) / 100) - 180;
  return {
    id: cleanName.toLowerCase().replace(/[^a-z0-9]/g, '_'),
    name: cleanName,
    country: 'GLOBAL OPS',
    coords: formatCoords(pseudoLat, pseudoLng),
    lat: pseudoLat,
    lng: pseudoLng,
    district: `${cleanName.toUpperCase()} // TACTICAL SECTOR`,
    mission: `OPERATION ${cleanName.toUpperCase()} RECON`,
    target: `Primary Target · ${cleanName}`,
    rangeM: 180 + (h % 150),
    wind: '2.4 M/S [←]',
    elev: '+35 M',
    nearby: [
      { name: `${cleanName} Station Terminal`, dist: '1.2 KM', dir: 'N', dx: 0, dz: -220 },
      { name: `${cleanName} Central Plaza`, dist: '1.9 KM', dir: 'SW', dx: -240, dz: 180 },
      { name: `${cleanName} Highway Junction`, dist: '2.6 KM', dir: 'SE', dx: 230, dz: 240 }
    ],
    isFallback: true
  };
}

export function getCityForDate(date = new Date()) {
  const dayOfYear = Math.floor((date - new Date(date.getFullYear(), 0, 0)) / (1000 * 60 * 60 * 24));
  return CITIES[dayOfYear % CITIES.length];
}

export function findCity(query) {
  return resolveLocation(query);
}


