import * as dotenv from 'dotenv';
import * as path from 'path';
import { Client } from 'pg';

dotenv.config({ path: path.resolve(__dirname, '.env.test') });
dotenv.config({ path: path.resolve(__dirname, '.env') });

const CONNECTION_STRING =
  process.env.DB_CONNECTION_STRING ??
  'postgresql://postgres.umdfumtqtvsbxzqcaixd:2026project@aws-1-ap-northeast-1.pooler.supabase.com:5432/postgres';

async function setup() {
  const client = new Client({ connectionString: CONNECTION_STRING });
  await client.connect();

  try {
    const mallRes = await client.query<{ id: string; name: string }>(
      `SELECT id::text, name FROM malls LIMIT 1`,
    );
    if (mallRes.rows.length === 0) {
      throw new Error('No rows in "malls" table. Seed at least one Mall first.');
    }
    const mall = mallRes.rows[0];
    process.env.TEST_MALL_ID = mall.id;
    console.log(`[setup] Mall: "${mall.name}" → ${mall.id}`);

    const storeRes = await client.query<{ id: string; name: string }>(
      `SELECT id::text, name FROM stores WHERE mall_id = $1 LIMIT 1`,
      [mall.id],
    );
    if (storeRes.rows.length === 0) {
      throw new Error(`No stores for mall "${mall.name}". Seed at least one Store.`);
    }
    const store = storeRes.rows[0];
    process.env.TEST_STORE_ID = store.id;
    console.log(`[setup] Store: "${store.name}" → ${store.id}`);

    const mgrRes = await client.query<{ id: string; name: string }>(
      `SELECT id::text, name FROM managers WHERE mall_id = $1 LIMIT 1`,
      [mall.id],
    );
    if (mgrRes.rows.length === 0) {
      throw new Error(`No managers for mall "${mall.name}". Seed at least one Manager.`);
    }
    process.env.TEST_MANAGER_ID = mgrRes.rows[0].id;
    console.log(`[setup] Manager: "${mgrRes.rows[0].name}" → ${mgrRes.rows[0].id}`);

  } finally {
    await client.end();
  }
}

export default setup;
