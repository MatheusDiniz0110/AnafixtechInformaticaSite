// Aviso de cookies — aceitar, recusar não essenciais e configurar.
// A escolha fica no localStorage do navegador. Não há cookie de consentimento.
//
// Quando o site passar a usar Analytics ou Pixel, carregue os scripts Somente se
// a preferência correspondente estiver true (window.CookieConsent.preferencias).
(function () {
    'use strict';

    var CHAVE = 'anafixtech.cookie-consent.v1';

    var banner = document.getElementById('cookie-banner');
    var modal = document.getElementById('cookie-modal');
    var cbAnalytics = document.getElementById('cookie-analytics');
    var cbAds = document.getElementById('cookie-ads');

    if (!banner || !modal || !cbAnalytics || !cbAds) return;

    function ler() {
        try {
            var bruto = localStorage.getItem(CHAVE);
            if (!bruto) return null;
            var dados = JSON.parse(bruto);
            return dados && typeof dados === 'object' ? dados : null;
        } catch (erro) {
            // Navegador com armazenamento bloqueado: trata como "sem escolha".
            return null;
        }
    }

    function salvar(analytics, ads) {
        var preferencia = {
            analytics: !!analytics,
            ads: !!ads,
            data: new Date().toISOString()
        };

        try {
            localStorage.setItem(CHAVE, JSON.stringify(preferencia));
        } catch (erro) {
            // Mesmo sem gravar, esconde o aviso para não travar a navegação.
        }

        window.CookieConsent = { preferencias: preferencia };
        fecharTudo();
    }

    function fecharTudo() {
        banner.hidden = true;
        modal.hidden = true;
    }

    function abrirConfig() {
        var atual = ler();
        cbAnalytics.checked = !!(atual && atual.analytics);
        cbAds.checked = !!(atual && atual.ads);
        banner.hidden = true;
        modal.hidden = false;
        cbAnalytics.focus();
    }

    function fecharConfig() {
        modal.hidden = true;
        if (!ler()) banner.hidden = false; // sem escolha, volta ao aviso
    }

    // Um único handler serve para os botões do banner, do painel, do rodapé
    // e da Política de Privacidade (basta usar data-cookie-action).
    document.addEventListener('click', function (evento) {
        var botao = evento.target.closest('[data-cookie-action]');
        if (!botao) return;

        switch (botao.getAttribute('data-cookie-action')) {
            case 'accept':
                salvar(true, true);
                break;
            case 'reject':
                salvar(false, false);
                break;
            case 'save':
                salvar(cbAnalytics.checked, cbAds.checked);
                break;
            case 'configure':
                abrirConfig();
                break;
            case 'close':
                fecharConfig();
                break;
        }
    });

    document.addEventListener('keydown', function (evento) {
        if (evento.key === 'Escape' && !modal.hidden) fecharConfig();
    });

    var escolha = ler();
    window.CookieConsent = { preferencias: escolha };

    if (escolha) {
        cbAnalytics.checked = !!escolha.analytics;
        cbAds.checked = !!escolha.ads;
        banner.hidden = true;
    } else {
        banner.hidden = false;
    }
})();
