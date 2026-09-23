import { defineConfig } from '@playwright/test';
export default defineConfig({ testDir:'./e2e', use:{channel:process.env.PW_CHANNEL || undefined,baseURL:'http://127.0.0.1:5173',viewport:{width:1440,height:1000}},workers:1,reporter:'list' });
