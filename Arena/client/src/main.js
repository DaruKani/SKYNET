import { createApp, h, ref, watch } from 'vue';
import {
    NConfigProvider,
    NMessageProvider,
    darkTheme,
    dateEnUS,
    enUS,
} from 'naive-ui';
import App from './App.vue';
import './styles.css';

const componentThemeOverrides = {
    Button: {
        borderRadiusMedium: '10px',
        heightMedium: '40px',
    },
    Card: {
        borderRadius: '16px',
    },
    Input: {
        borderRadius: '10px',
    },
};

const darkThemeOverrides = {
    ...componentThemeOverrides,
    Radio: {
        buttonColorActive: '#ff6875',
        buttonTextColorActive: '#0d1017',
        buttonBorderColorActive: '#ff6875',
    },
    common: {
        bodyColor: '#0d1017',
        cardColor: '#151922',
        primaryColor: '#ff6875',
        primaryColorHover: '#ff98a1',
        primaryColorPressed: '#e84a5b',
        successColor: '#70e29a',
        warningColor: '#f2c56d',
        errorColor: '#ff7b7b',
        borderColor: '#21343d',
        textColorBase: '#e7f0f3',
    },
};

const lightThemeOverrides = {
    ...componentThemeOverrides,
    Radio: {
        buttonColorActive: '#b32941',
        buttonTextColorActive: '#ffffff',
        buttonBorderColorActive: '#b32941',
    },
    common: {
        bodyColor: '#f2f7f7',
        cardColor: '#ffffff',
        primaryColor: '#b32941',
        primaryColorHover: '#c33b52',
        primaryColorPressed: '#962137',
        successColor: '#218950',
        warningColor: '#ad7316',
        errorColor: '#c94747',
        borderColor: '#cbdadc',
        textColorBase: '#102a32',
        textColor1: '#102a32',
        textColor2: '#29464f',
        textColor3: '#4d666e',
        placeholderColor: '#6b8187',
    },
};

let initialDarkMode = true;
try {
    initialDarkMode = (localStorage.getItem('skynet-arena-theme')
        || localStorage.getItem('windbot-arena-theme') || 'dark') === 'dark';
} catch {
    // Theme switching remains available when browser storage is disabled.
}

createApp({
    setup() {
        const darkMode = ref(initialDarkMode);
        watch(darkMode, (enabled) => {
            const theme = enabled ? 'dark' : 'light';
            document.documentElement.dataset.theme = theme;
            document.documentElement.style.colorScheme = theme;
            document.querySelector('meta[name="theme-color"]')
                ?.setAttribute('content', enabled ? '#0d1017' : '#eef4f4');
            try {
                localStorage.setItem('skynet-arena-theme', theme);
            } catch {
                // Switching themes does not depend on browser storage.
            }
        }, { immediate: true });

        return () => h(NConfigProvider, {
            dateLocale: dateEnUS,
            locale: enUS,
            theme: darkMode.value ? darkTheme : null,
            themeOverrides: darkMode.value ? darkThemeOverrides : lightThemeOverrides,
        }, {
            default: () => h(NMessageProvider, { placement: 'top-right' }, {
                default: () => h(App, {
                    darkMode: darkMode.value,
                    'onUpdate:darkMode': (enabled) => { darkMode.value = enabled; },
                }),
            }),
        });
    },
}).mount('#app');
