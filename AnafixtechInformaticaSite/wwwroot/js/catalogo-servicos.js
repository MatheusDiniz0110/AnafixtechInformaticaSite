(function () {
    var filtros = document.querySelectorAll('#filtro-tabs .filtro');
    var servicos = document.querySelectorAll('#lista-servicos .servico');
    var grupos = document.querySelectorAll('#lista-servicos [data-grupo]');
    var busca = document.getElementById('busca-servicos');
    var resultado = document.getElementById('resultado-servicos');
    var semResultados = document.getElementById('sem-resultados');

    if (!busca || servicos.length === 0) {
        return;
    }

    var categoriaAtual = 'Todos';
    var termoAtual = '';

    function normalizar(texto) {
        return texto.toLowerCase().normalize('NFD').replace(/[\u0300-\u036f]/g, '');
    }

    function aplicarFiltros() {
        var visiveis = 0;

        servicos.forEach(function (item) {
            var daCategoria = categoriaAtual === 'Todos' || item.dataset.categoria === categoriaAtual;
            var doTexto = termoAtual === '' || normalizar(item.dataset.busca).indexOf(termoAtual) !== -1;
            var mostrar = daCategoria && doTexto;

            item.hidden = !mostrar;
            if (mostrar) {
                visiveis++;
            }
        });

        grupos.forEach(function (grupo) {
            var temItemVisivel = grupo.querySelector('.servico:not([hidden])') !== null;
            grupo.hidden = !temItemVisivel;
        });

        if (resultado) {
            resultado.textContent = visiveis + (visiveis === 1 ? ' serviço' : ' serviços');
        }

        if (semResultados) {
            semResultados.hidden = visiveis !== 0;
        }
    }

    filtros.forEach(function (botao) {
        botao.addEventListener('click', function () {
            filtros.forEach(function (outro) {
                outro.classList.remove('ativo');
                outro.setAttribute('aria-pressed', 'false');
            });

            botao.classList.add('ativo');
            botao.setAttribute('aria-pressed', 'true');
            categoriaAtual = botao.dataset.filtro;
            aplicarFiltros();
        });
    });

    busca.addEventListener('input', function () {
        termoAtual = normalizar(busca.value.trim());
        aplicarFiltros();
    });

    var categoriaInicial = new URLSearchParams(window.location.search).get('categoria');
    if (categoriaInicial) {
        var alvo = Array.prototype.find.call(filtros, function (botao) {
            return botao.dataset.filtro === categoriaInicial;
        });

        if (alvo) {
            alvo.click();
        }
    }
})();
