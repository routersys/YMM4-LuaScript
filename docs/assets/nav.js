(function () {
  var NAV = [
    {
      group: "はじめに",
      items: [
        { title: "概要", href: "index.html" },
        { title: "導入と使い方", href: "getting-started.html" }
      ]
    },
    {
      group: "Lua 言語",
      items: [
        { title: "Lua の基礎", href: "lua-basics.html" },
        { title: "標準ライブラリ", href: "stdlib.html" }
      ]
    },
    {
      group: "API リファレンス",
      items: [
        { title: "グローバル変数", href: "globals.html" },
        { title: "obj オブジェクト", href: "obj.html" },
        { title: "obj の関数", href: "obj-functions.html" },
        { title: "anim ライブラリ", href: "anim.html" },
        { title: "scene と ymm4", href: "scene-ymm4.html" },
        { title: "ピクセル操作", href: "pixels.html" }
      ]
    },
    {
      group: "高度な機能",
      items: [
        { title: "エンジンと高速化", href: "engines.html" },
        { title: "パラメータ指定子", href: "directives.html" }
      ]
    },
    {
      group: "実用",
      items: [
        { title: "サンプル集", href: "samples.html" },
        { title: "クイックリファレンス", href: "reference.html" },
        { title: "制限事項とトラブル", href: "limits.html" }
      ]
    }
  ];

  function currentFile() {
    var path = window.location.pathname;
    var file = path.substring(path.lastIndexOf("/") + 1);
    return file === "" ? "index.html" : file;
  }

  function build() {
    var here = currentFile();
    var sidebar = document.getElementById("sidebar");
    if (!sidebar) return;

    var html = '<a class="brand" href="index.html">Lua スクリプト for YMM4</a>';
    html += '<input id="navfilter" type="search" placeholder="ページを検索" aria-label="ページを検索">';

    for (var i = 0; i < NAV.length; i++) {
      html += '<div class="group">' + NAV[i].group + "</div><ul>";
      for (var j = 0; j < NAV[i].items.length; j++) {
        var it = NAV[i].items[j];
        var cls = it.href === here ? ' class="active"' : "";
        html += "<li><a" + cls + ' href="' + it.href + '">' + it.title + "</a></li>";
      }
      html += "</ul>";
    }
    sidebar.innerHTML = html;

    var filter = document.getElementById("navfilter");
    filter.addEventListener("input", function () {
      var q = filter.value.trim().toLowerCase();
      var links = sidebar.querySelectorAll("li a");
      for (var k = 0; k < links.length; k++) {
        var match = links[k].textContent.toLowerCase().indexOf(q) !== -1;
        links[k].parentNode.style.display = match ? "" : "none";
      }
      var groups = sidebar.querySelectorAll(".group");
      for (var g = 0; g < groups.length; g++) {
        var ul = groups[g].nextElementSibling;
        var any = ul.querySelectorAll('li:not([style*="none"])').length > 0;
        groups[g].style.display = any ? "" : "none";
      }
    });
  }

  function toggle() {
    var btn = document.getElementById("navtoggle");
    if (!btn) return;
    btn.addEventListener("click", function () {
      document.body.classList.toggle("nav-open");
    });
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", function () { build(); toggle(); });
  } else {
    build();
    toggle();
  }
})();
