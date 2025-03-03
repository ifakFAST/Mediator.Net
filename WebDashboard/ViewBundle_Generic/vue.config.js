const CopyWebpackPlugin = require('copy-webpack-plugin');
const path = require('path');

module.exports = {
  publicPath: '/ViewBundle_Generic/',
  outputDir: '../../Run/DashboardDist/ViewBundle_Generic',
  configureWebpack: {
    performance: { hints: false },
    plugins: [
      new CopyWebpackPlugin({
        patterns: [
          {
            from: path.resolve(__dirname, 'src/assets/images'),
            to: path.resolve(__dirname, '../../Run/DashboardDist/ViewBundle_Generic/images')
          }
        ]
      })
    ]
  },
  pages: {
    variables: 'src/view_variables/main.ts',
    eventlog:  'src/view_eventlog/main.ts',
    history:   'src/view_history/main.ts',
    generic:   'src/view_generic/main.ts',
    calc:      'src/view_calc/main.ts',
    pages:     'src/view_pages/main.ts',
  },
  transpileDependencies: [
    "vuetify"
  ],
  css: {
    extract: { ignoreOrder: true },
    loaderOptions: {
      sass: {
        warnRuleAsWarning: false
      },
    }
  },
}